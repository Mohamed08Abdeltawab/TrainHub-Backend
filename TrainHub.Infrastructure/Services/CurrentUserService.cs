using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TrainHub.Application.Common.Exceptions;
using TrainHub.Application.Features.Users.DTOs;
using TrainHub.Application.Interfaces;
using TrainHub.Domain.Models;
using TrainHub.Infrastructure.Data;

namespace TrainHub.Infrastructure.Services;

/// <summary>
/// Infrastructure implementation of <see cref="ICurrentUserService"/> using ASP.NET Core Identity.
/// </summary>
public class CurrentUserService : ICurrentUserService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly TrainHubDbContext _dbContext;
    private readonly ILogger<CurrentUserService> _logger;

    public CurrentUserService(
        UserManager<ApplicationUser> userManager,
        TrainHubDbContext dbContext,
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
}
