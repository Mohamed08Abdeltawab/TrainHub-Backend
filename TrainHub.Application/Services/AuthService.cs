using System.Security.Claims;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TrainHub.Application.Common.Exceptions;
using TrainHub.Application.Common.Security;
using TrainHub.Application.Features.Auth.DTOs;
using TrainHub.Application.Interfaces;
using TrainHub.Domain.Constants;
using TrainHub.Domain.Models;

namespace TrainHub.Application.Services;

/// <summary>
/// Service implementation of IAuthService using ASP.NET Core Identity and JWT token generation.
/// </summary>
public class AuthService : IAuthService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IJwtTokenGenerator _jwtTokenGenerator;
    private readonly ITrainHubDbContext _dbContext;
    private readonly JwtSettings _jwtSettings;
    private readonly IOrganizationValidator _organizationValidator;
    private readonly ILogger<AuthService> _logger;
    private readonly IHttpContextAccessor? _httpContextAccessor;

    public AuthService(
        UserManager<ApplicationUser> userManager,
        IJwtTokenGenerator jwtTokenGenerator,
        ITrainHubDbContext dbContext,
        IOptions<JwtSettings> jwtOptions,
        IOrganizationValidator organizationValidator,
        ILogger<AuthService> logger,
        IHttpContextAccessor? httpContextAccessor = null)
    {
        _userManager = userManager;
        _jwtTokenGenerator = jwtTokenGenerator;
        _dbContext = dbContext;
        _jwtSettings = jwtOptions.Value;
        _organizationValidator = organizationValidator;
        _logger = logger;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        _logger.LogInformation("Registration attempt for email '{Email}'.", request.Email);

        // ── 1. Guard against unauthorized administrative role escalation & unauthenticated instructor provisioning ──
        if (!string.IsNullOrWhiteSpace(request.Role))
        {
            var requestedRole = request.Role.Trim();
            if (requestedRole.Equals(AppRoles.SuperAdmin, StringComparison.OrdinalIgnoreCase) ||
                requestedRole.Equals(AppRoles.OrgAdmin, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("Attempted unauthorized role escalation to '{Role}' during registration.", requestedRole);
                throw new BadRequestException("Administrative roles cannot be assigned via registration.");
            }

            if (requestedRole.Equals(AppRoles.Instructor, StringComparison.OrdinalIgnoreCase))
            {
                var caller = _httpContextAccessor?.HttpContext?.User;
                var isStaffAdmin = caller?.Identity?.IsAuthenticated == true &&
                    (caller.IsInRole(AppRoles.SuperAdmin) ||
                     caller.IsInRole(AppRoles.OrgAdmin) ||
                     caller.HasClaim(c => (c.Type == ClaimTypes.Role || c.Type == "role") &&
                                          (c.Value == AppRoles.SuperAdmin || c.Value == AppRoles.OrgAdmin)));

                if (!isStaffAdmin)
                {
                    _logger.LogWarning("Anonymous or unauthorized attempt to self-register as '{Role}'.", requestedRole);
                    throw new BadRequestException("Instructor accounts can only be provisioned by authorized organization administrators.");
                }
            }
            else if (!requestedRole.Equals(AppRoles.Student, StringComparison.OrdinalIgnoreCase))
            {
                throw new BadRequestException($"Invalid registration role '{requestedRole}'.");
            }
        }

        var hasExplicitOrgId = request.OrganizationId.HasValue && request.OrganizationId.Value != Guid.Empty;
        var orgNameInput = (request.OrganizationName ?? request.Organization)?.Trim();

        if (!hasExplicitOrgId && string.IsNullOrWhiteSpace(orgNameInput))
        {
            throw new BadRequestException("Either an existing Organization ID or a new Organization Name must be provided.");
        }

        Guid targetOrgId;
        string targetOrgName;
        string assignedRole;
        Organization? createdOrg = null;
        ApplicationUser? createdUser = null;
        Student? createdStudent = null;

        Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction? transaction = null;
        try
        {
            if (_dbContext.Database.ProviderName?.Contains("InMemory", StringComparison.OrdinalIgnoreCase) != true)
            {
                transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
            }
        }
        catch
        {
            // Transactions not supported on current database provider (e.g. test provider)
        }

        try
        {
            if (!hasExplicitOrgId)
            {
                // ── Mode A: Self-service Training Center Onboarding ──
                var baseSlug = Regex.Replace(orgNameInput!.ToLowerInvariant(), @"[^a-z0-9]+", "-").Trim('-');
                if (string.IsNullOrWhiteSpace(baseSlug)) baseSlug = "org";
                var slug = baseSlug;
                var suffix = 1;
                while (await _dbContext.Organizations.IgnoreQueryFilters().AnyAsync(o => o.Slug == slug, cancellationToken))
                {
                    slug = $"{baseSlug}-{suffix++}";
                }

                createdOrg = new Organization
                {
                    Id = Guid.NewGuid(),
                    Name = orgNameInput,
                    Slug = slug,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                await _dbContext.Organizations.AddAsync(createdOrg, cancellationToken);
                await _dbContext.SaveChangesAsync(cancellationToken);

                targetOrgId = createdOrg.Id;
                targetOrgName = createdOrg.Name;
                assignedRole = AppRoles.OrgAdmin;
            }
            else
            {
                // ── Mode B: Registering into an Existing Organization ──
                targetOrgId = request.OrganizationId!.Value;
                await _organizationValidator.ValidateOrganizationActiveAsync(targetOrgId, cancellationToken);

                var org = await _dbContext.Organizations
                    .IgnoreQueryFilters()
                    .AsNoTracking()
                    .FirstOrDefaultAsync(o => o.Id == targetOrgId, cancellationToken);

                if (org != null)
                {
                    if (org.Slug.Equals("platform", StringComparison.OrdinalIgnoreCase))
                    {
                        throw new BadRequestException("Registration into the platform organization is not allowed.");
                    }
                    targetOrgName = org.Name;
                }
                else
                {
                    targetOrgName = "Organization";
                }
                assignedRole = string.IsNullOrWhiteSpace(request.Role) ? AppRoles.Student : request.Role.Trim();
            }

            // ── Check for duplicate email within the organization ──
            var normalizedEmail = _userManager.NormalizeEmail(request.Email);
            var existingUser = await _dbContext.ApplicationUsers
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(u => u.NormalizedEmail == normalizedEmail && u.OrganizationId == targetOrgId, cancellationToken);

            if (existingUser != null)
            {
                _logger.LogWarning("Email '{Email}' is already registered in organization '{OrganizationId}'.",
                    request.Email, targetOrgId);
                throw new ConflictException($"A user with email '{request.Email}' already exists in this organization.");
            }

            // ── Create ApplicationUser ──
            var user = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                FirstName = request.FirstName.Trim(),
                LastName = request.LastName.Trim(),
                Email = request.Email.Trim(),
                UserName = request.Email.Trim(),
                OrganizationId = targetOrgId,
                PhoneNumber = request.PhoneNumber?.Trim(),
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            var createResult = await _userManager.CreateAsync(user, request.Password);
            if (!createResult.Succeeded)
            {
                var errors = string.Join("; ", createResult.Errors.Select(e => e.Description));
                _logger.LogError("Failed to create user '{Email}'. Errors: {Errors}", request.Email, errors);
                throw new BadRequestException($"Registration failed: {errors}");
            }
            createdUser = user;

            // ── Assign Role ──
            var roleResult = await _userManager.AddToRoleAsync(user, assignedRole);
            if (!roleResult.Succeeded)
            {
                var errors = string.Join("; ", roleResult.Errors.Select(e => e.Description));
                _logger.LogError("Failed to assign role '{Role}' to user '{UserId}'. Errors: {Errors}", assignedRole, user.Id, errors);
                throw new BadRequestException($"Failed to assign role: {errors}");
            }

            // ── If Student, create Student domain entity to prevent orphan account (WRK-01) ──
            if (assignedRole.Equals(AppRoles.Student, StringComparison.OrdinalIgnoreCase))
            {
                var studentCode = !string.IsNullOrWhiteSpace(request.StudentCode)
                    ? request.StudentCode.Trim()
                    : $"STU-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}";

                createdStudent = Student.Create(
                    user.Id,
                    targetOrgId,
                    studentCode,
                    guardianPhone: request.GuardianPhone?.Trim() ?? request.PhoneNumber?.Trim(),
                    academicYear: request.AcademicYear?.Trim(),
                    dateOfBirth: null,
                    notes: "Registered student");

                await _dbContext.Students.AddAsync(createdStudent, cancellationToken);
                await _dbContext.SaveChangesAsync(cancellationToken);
            }

            // ── Generate Tokens ──
            var roles = new List<string> { assignedRole };
            var accessToken = _jwtTokenGenerator.GenerateToken(user, roles);
            var refreshToken = _jwtTokenGenerator.GenerateRefreshToken();
            user.RefreshToken = refreshToken;
            user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(_jwtSettings.RefreshTokenExpiryDays);

            await _userManager.UpdateAsync(user);

            if (transaction != null)
            {
                await transaction.CommitAsync(cancellationToken);
            }

            _logger.LogInformation("Registration completed successfully for user '{UserId}' in organization '{OrgId}'.", user.Id, targetOrgId);

            return new AuthResponse
            {
                UserId = user.Id,
                StudentId = createdStudent?.Id,
                StudentCode = createdStudent?.StudentCode,
                OrganizationId = targetOrgId,
                OrganizationName = targetOrgName,
                Email = user.Email!,
                UserName = user.UserName!,
                FirstName = user.FirstName,
                LastName = user.LastName,
                AccessToken = accessToken,
                RefreshToken = refreshToken,
                AccessTokenExpiry = DateTime.UtcNow.AddMinutes(
                    _jwtSettings.ExpiryMinutes > 0 ? _jwtSettings.ExpiryMinutes : 60),
                Roles = roles
            };
        }
        catch
        {
            if (transaction != null)
            {
                await transaction.RollbackAsync(cancellationToken);
            }
            else
            {
                // In-memory or non-relational compensating rollback
                if (createdStudent != null)
                {
                    _dbContext.Students.Remove(createdStudent);
                    await _dbContext.SaveChangesAsync(CancellationToken.None);
                }
                if (createdUser != null)
                {
                    await _userManager.DeleteAsync(createdUser);
                }
                if (createdOrg != null)
                {
                    _dbContext.Organizations.Remove(createdOrg);
                    await _dbContext.SaveChangesAsync(CancellationToken.None);
                }
            }
            throw;
        }
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Login attempt for email '{Email}' in organization '{OrganizationId}'.",
            request.Email, request.OrganizationId);

        var normalizedEmail = _userManager.NormalizeEmail(request.Email);

        ApplicationUser? user;
        if (request.OrganizationId.HasValue && request.OrganizationId.Value != Guid.Empty)
        {
            user = await _dbContext.ApplicationUsers
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(u => u.NormalizedEmail == normalizedEmail && u.OrganizationId == request.OrganizationId.Value, cancellationToken);
        }
        else
        {
            var matchingUsers = await _dbContext.ApplicationUsers
                .IgnoreQueryFilters()
                .Where(u => u.NormalizedEmail == normalizedEmail)
                .Take(2)
                .ToListAsync(cancellationToken);

            if (matchingUsers.Count == 0)
            {
                _logger.LogWarning("Login failed: User '{Email}' not found.", request.Email);
                throw new UnauthorizedAccessException("Invalid email or password.");
            }

            if (matchingUsers.Count > 1)
            {
                _logger.LogWarning("Login failed: Multiple accounts found for email '{Email}' across organizations.", request.Email);
                throw new BadRequestException("Multiple accounts found for this email. Please specify Organization ID.");
            }

            user = matchingUsers[0];
        }

        if (user == null)
        {
            _logger.LogWarning("Login failed: User '{Email}' not found in organization '{OrganizationId}'.", request.Email, request.OrganizationId);
            throw new UnauthorizedAccessException("Invalid email or password.");
        }

        if (!user.IsActive)
        {
            _logger.LogWarning("Login failed: User '{UserId}' is inactive.", user.Id);
            throw new UnauthorizedAccessException("Your account is currently inactive.");
        }

        // ── Validate Organization is active ─────────────────────────
        await _organizationValidator.ValidateOrganizationActiveAsync(user.OrganizationId, cancellationToken);

        var isPasswordValid = await _userManager.CheckPasswordAsync(user, request.Password);
        if (!isPasswordValid)
        {
            _logger.LogWarning("Login failed: Invalid password for user '{UserId}'.", user.Id);
            throw new UnauthorizedAccessException("Invalid email or password.");
        }

        var roles = await _userManager.GetRolesAsync(user);

        var accessToken = _jwtTokenGenerator.GenerateToken(user, roles);
        var refreshToken = _jwtTokenGenerator.GenerateRefreshToken();

        user.RefreshToken = refreshToken;
        user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(_jwtSettings.RefreshTokenExpiryDays);

        await _userManager.UpdateAsync(user);

        _logger.LogInformation("Login successful for user '{UserId}'.", user.Id);

        var org = await _dbContext.Organizations
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.Id == user.OrganizationId, cancellationToken);

        return new AuthResponse
        {
            UserId = user.Id,
            OrganizationId = user.OrganizationId,
            OrganizationName = org?.Name,
            Email = user.Email!,
            UserName = user.UserName!,
            FirstName = user.FirstName,
            LastName = user.LastName,
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            AccessTokenExpiry = DateTime.UtcNow.AddMinutes(
                _jwtSettings.ExpiryMinutes > 0 ? _jwtSettings.ExpiryMinutes : 60),
            Roles = roles.ToList()
        };
    }

    public async Task<AuthResponse> RefreshTokenAsync(RefreshTokenRequest request, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Refresh token attempt for user '{UserId}'.", request.UserId);

        var user = await _userManager.FindByIdAsync(request.UserId.ToString());

        if (user == null || user.RefreshToken != request.RefreshToken)
        {
            _logger.LogWarning("Refresh token failed for user '{UserId}': Invalid token.", request.UserId);
            throw new UnauthorizedAccessException("Invalid refresh token.");
        }

        if (user.RefreshTokenExpiryTime <= DateTime.UtcNow)
        {
            _logger.LogWarning("Refresh token failed for user '{UserId}': Token expired.", request.UserId);
            throw new UnauthorizedAccessException("Refresh token has expired. Please log in again.");
        }

        if (!user.IsActive)
        {
            _logger.LogWarning("Refresh token failed: User '{UserId}' is inactive.", user.Id);
            throw new UnauthorizedAccessException("Your account is currently inactive.");
        }

        // ── Validate Organization is active ─────────────────────────
        await _organizationValidator.ValidateOrganizationActiveAsync(user.OrganizationId, cancellationToken);

        var roles = await _userManager.GetRolesAsync(user);

        var accessToken = _jwtTokenGenerator.GenerateToken(user, roles);
        var newRefreshToken = _jwtTokenGenerator.GenerateRefreshToken();

        user.RefreshToken = newRefreshToken;
        user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(_jwtSettings.RefreshTokenExpiryDays);

        await _userManager.UpdateAsync(user);

        _logger.LogInformation("Refresh token successful for user '{UserId}'.", user.Id);

        var org = await _dbContext.Organizations
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.Id == user.OrganizationId, cancellationToken);

        return new AuthResponse
        {
            UserId = user.Id,
            OrganizationId = user.OrganizationId,
            OrganizationName = org?.Name,
            Email = user.Email!,
            UserName = user.UserName!,
            FirstName = user.FirstName,
            LastName = user.LastName,
            AccessToken = accessToken,
            RefreshToken = newRefreshToken,
            AccessTokenExpiry = DateTime.UtcNow.AddMinutes(
                _jwtSettings.ExpiryMinutes > 0 ? _jwtSettings.ExpiryMinutes : 60),
            Roles = roles.ToList()
        };
    }

    public async Task LogoutAsync(LogoutRequest request, CancellationToken cancellationToken = default)
    {
        var targetUserId = request.UserId ?? Guid.Empty;
        _logger.LogInformation("Logout attempt for user '{UserId}'.", targetUserId);

        var user = await _userManager.FindByIdAsync(targetUserId.ToString());

        if (user == null)
        {
            _logger.LogWarning("Logout failed: User '{UserId}' not found.", targetUserId);
            return;
        }

        // Only revoke if the provided refresh token matches the current one in the database (or if not provided)
        if (string.IsNullOrEmpty(request.RefreshToken) || user.RefreshToken == request.RefreshToken)
        {
            user.RefreshToken = null;
            user.RefreshTokenExpiryTime = DateTime.UtcNow;

            await _userManager.UpdateAsync(user);
            _logger.LogInformation("Logout successful for user '{UserId}'. Refresh token revoked.", targetUserId);
        }
        else
        {
            _logger.LogWarning("Logout failed for user '{UserId}': Mismatched refresh token.", targetUserId);
            throw new UnauthorizedAccessException("Invalid refresh token.");
        }
    }
}
