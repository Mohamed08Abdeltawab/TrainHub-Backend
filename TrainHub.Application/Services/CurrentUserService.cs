using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TrainHub.Application.Common.Exceptions;
using TrainHub.Application.Features.Users.DTOs;
using TrainHub.Application.Interfaces;
using TrainHub.Domain.Models;

namespace TrainHub.Application.Services;

/// <summary>
/// Service implementation of <see cref="ICurrentUserService"/> using ASP.NET Core Identity.
/// </summary>
public class CurrentUserService : ICurrentUserService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ITrainHubDbContext _dbContext;
    private readonly ILogger<CurrentUserService> _logger;

    public CurrentUserService(
        UserManager<ApplicationUser> userManager,
        ITrainHubDbContext dbContext,
        ILogger<CurrentUserService> logger)
    {
        _userManager = userManager;
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<CurrentUserResponse> GetCurrentUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Retrieving profile for current user '{UserId}'.", userId);

        var user = await _dbContext.ApplicationUsers
            .Include(u => u.Organization)
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

        if (user is null)
        {
            _logger.LogWarning("Current user '{UserId}' was not found.", userId);
            throw new NotFoundException("User", userId);
        }

        if (!user.IsActive)
        {
            _logger.LogWarning("Current user '{UserId}' is inactive.", userId);
            throw new UnauthorizedAccessException("Your account is currently inactive.");
        }

        var roles = await _userManager.GetRolesAsync(user);

        _logger.LogInformation("Profile retrieved successfully for user '{UserId}'.", userId);

        return new CurrentUserResponse
        {
            UserId = user.Id,
            Email = user.Email ?? string.Empty,
            UserName = user.UserName ?? string.Empty,
            FirstName = user.FirstName,
            LastName = user.LastName,
            PhoneNumber = user.PhoneNumber,
            OrganizationId = user.OrganizationId,
            OrganizationName = user.Organization?.Name ?? string.Empty,
            IsActive = user.IsActive,
            Roles = roles.ToList(),
            CreatedAt = user.CreatedAt,
            UpdatedAt = user.UpdatedAt
        };
    }

    public async Task<CurrentUserResponse> UpdateProfileAsync(Guid userId, UpdateCurrentUserDto dto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        _logger.LogInformation("Updating profile for current user '{UserId}'.", userId);

        var user = await _dbContext.ApplicationUsers
            .Include(u => u.Organization)
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

        if (user is null)
        {
            _logger.LogWarning("Current user '{UserId}' was not found for update.", userId);
            throw new NotFoundException("User", userId);
        }

        if (!user.IsActive)
        {
            _logger.LogWarning("Current user '{UserId}' is inactive.", userId);
            throw new UnauthorizedAccessException("Your account is currently inactive.");
        }

        if (!string.IsNullOrWhiteSpace(dto.FirstName))
            user.FirstName = dto.FirstName.Trim();

        if (!string.IsNullOrWhiteSpace(dto.LastName))
            user.LastName = dto.LastName.Trim();

        if (dto.PhoneNumber != null)
            user.PhoneNumber = string.IsNullOrWhiteSpace(dto.PhoneNumber) ? null : dto.PhoneNumber.Trim();

        user.UpdatedAt = DateTime.UtcNow;

        var updateResult = await _userManager.UpdateAsync(user);
        if (!updateResult.Succeeded)
        {
            var errors = string.Join("; ", updateResult.Errors.Select(e => e.Description));
            _logger.LogError("Failed to update user '{UserId}'. Errors: {Errors}", userId, errors);
            throw new BadRequestException($"Failed to update profile: {errors}");
        }

        var roles = await _userManager.GetRolesAsync(user);

        _logger.LogInformation("Profile updated successfully for user '{UserId}'.", userId);

        return new CurrentUserResponse
        {
            UserId = user.Id,
            Email = user.Email ?? string.Empty,
            UserName = user.UserName ?? string.Empty,
            FirstName = user.FirstName,
            LastName = user.LastName,
            PhoneNumber = user.PhoneNumber,
            OrganizationId = user.OrganizationId,
            OrganizationName = user.Organization?.Name ?? string.Empty,
            IsActive = user.IsActive,
            Roles = roles.ToList(),
            CreatedAt = user.CreatedAt,
            UpdatedAt = user.UpdatedAt
        };
    }
}
