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
using TrainHub.Infrastructure.Data;

namespace TrainHub.Infrastructure.Services;

/// <summary>
/// Infrastructure implementation of IAuthService using ASP.NET Core Identity.
/// </summary>
public class AuthService : IAuthService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IJwtTokenGenerator _jwtTokenGenerator;
    private readonly TrainHubDbContext _dbContext;
    private readonly JwtSettings _jwtSettings;
    private readonly IOrganizationValidator _organizationValidator;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        UserManager<ApplicationUser> userManager,
        IJwtTokenGenerator jwtTokenGenerator,
        TrainHubDbContext dbContext,
        IOptions<JwtSettings> jwtOptions,
        IOrganizationValidator organizationValidator,
        ILogger<AuthService> logger)
    {
        _userManager = userManager;
        _jwtTokenGenerator = jwtTokenGenerator;
        _dbContext = dbContext;
        _jwtSettings = jwtOptions.Value;
        _organizationValidator = organizationValidator;
        _logger = logger;
    }

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Registration attempt for email '{Email}' in organization '{OrganizationId}'.",
            request.Email, request.OrganizationId);

        // ── Validate Organization exists and is active ──────────────
        await _organizationValidator.ValidateOrganizationActiveAsync(request.OrganizationId, cancellationToken);

        // ── Check for duplicate email within the organization ───────
        var existingUser = await _userManager.FindByEmailAsync(request.Email);
        if (existingUser != null && existingUser.OrganizationId == request.OrganizationId)
        {
            _logger.LogWarning("Email '{Email}' is already registered in organization '{OrganizationId}'.",
                request.Email, request.OrganizationId);
            throw new ConflictException($"A user with email '{request.Email}' already exists in this organization.");
        }

        // ── Create the ApplicationUser ──────────────────────────────
        var user = new ApplicationUser
        {
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            Email = request.Email.Trim(),
            UserName = request.Email.Trim(),
            OrganizationId = request.OrganizationId,
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

        _logger.LogInformation("User '{UserId}' created successfully.", user.Id);

        // ── Assign default role ─────────────────────────────────────
        var roleResult = await _userManager.AddToRoleAsync(user, AppRoles.Student);
        if (!roleResult.Succeeded)
        {
            var errors = string.Join("; ", roleResult.Errors.Select(e => e.Description));
            _logger.LogError("Failed to assign default role to user '{UserId}'. Errors: {Errors}", user.Id, errors);
            throw new BadRequestException($"Failed to assign default role: {errors}");
        }

        var roles = new List<string> { AppRoles.Student };

        // ── Generate tokens ─────────────────────────────────────────
        var accessToken = _jwtTokenGenerator.GenerateToken(user, roles);

        var refreshToken = _jwtTokenGenerator.GenerateRefreshToken();
        user.RefreshToken = refreshToken;
        user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(_jwtSettings.RefreshTokenExpiryDays);

        await _userManager.UpdateAsync(user);

        _logger.LogInformation("Registration completed successfully for user '{UserId}'.", user.Id);

        return new AuthResponse
        {
            UserId = user.Id,
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

    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Login attempt for email '{Email}' in organization '{OrganizationId}'.",
            request.Email, request.OrganizationId);

        var normalizedEmail = _userManager.NormalizeEmail(request.Email);

        var user = await _dbContext.ApplicationUsers
            .FirstOrDefaultAsync(u => u.NormalizedEmail == normalizedEmail && u.OrganizationId == request.OrganizationId, cancellationToken);

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

        // ── Validate Organization is active and subscription healthy ─
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

        return new AuthResponse
        {
            UserId = user.Id,
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

        // ── Validate Organization is active and subscription healthy ─
        await _organizationValidator.ValidateOrganizationActiveAsync(user.OrganizationId, cancellationToken);

        var roles = await _userManager.GetRolesAsync(user);

        var accessToken = _jwtTokenGenerator.GenerateToken(user, roles);
        var newRefreshToken = _jwtTokenGenerator.GenerateRefreshToken();

        user.RefreshToken = newRefreshToken;
        user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(_jwtSettings.RefreshTokenExpiryDays);

        await _userManager.UpdateAsync(user);

        _logger.LogInformation("Refresh token successful for user '{UserId}'.", user.Id);

        return new AuthResponse
        {
            UserId = user.Id,
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
        _logger.LogInformation("Logout attempt for user '{UserId}'.", request.UserId);

        var user = await _userManager.FindByIdAsync(request.UserId.ToString());

        if (user == null)
        {
            _logger.LogWarning("Logout failed: User '{UserId}' not found.", request.UserId);
            return; // Idempotent: User already doesn't exist, treat as logged out.
        }

        // Only revoke if the provided refresh token matches the current one in the database
        if (user.RefreshToken == request.RefreshToken)
        {
            user.RefreshToken = null;
            user.RefreshTokenExpiryTime = DateTime.UtcNow;

            await _userManager.UpdateAsync(user);
            _logger.LogInformation("Logout successful for user '{UserId}'. Refresh token revoked.", request.UserId);
        }
        else
        {
            _logger.LogWarning("Logout failed for user '{UserId}': Mismatched refresh token.", request.UserId);
            throw new UnauthorizedAccessException("Invalid refresh token.");
        }
    }
}
