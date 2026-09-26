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
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        UserManager<ApplicationUser> userManager,
        IJwtTokenGenerator jwtTokenGenerator,
        TrainHubDbContext dbContext,
        IOptions<JwtSettings> jwtOptions,
        ILogger<AuthService> logger)
    {
        _userManager = userManager;
        _jwtTokenGenerator = jwtTokenGenerator;
        _dbContext = dbContext;
        _jwtSettings = jwtOptions.Value;
        _logger = logger;
    }

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Registration attempt for email '{Email}' in organization '{OrganizationId}'.",
            request.Email, request.OrganizationId);

        // ── Validate Organization exists ────────────────────────────
        var organizationExists = await _dbContext.Organizations
            .AnyAsync(o => o.Id == request.OrganizationId, cancellationToken);

        if (!organizationExists)
        {
            _logger.LogWarning("Organization '{OrganizationId}' was not found during registration.", request.OrganizationId);
            throw new NotFoundException("Organization", request.OrganizationId);
        }

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
}
