using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TrainHub.Application.Common.Exceptions;
using TrainHub.Application.Features.Roles.DTOs;
using TrainHub.Application.Interfaces;
using TrainHub.Domain.Constants;
using TrainHub.Domain.Models;

namespace TrainHub.Application.Services;

/// <summary>
/// Service implementation of IRoleService using ASP.NET Core Identity.
/// </summary>
public class RoleService : IRoleService
{
    private readonly RoleManager<IdentityRole<Guid>> _roleManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<RoleService> _logger;

    public RoleService(
        RoleManager<IdentityRole<Guid>> roleManager,
        UserManager<ApplicationUser> userManager,
        ILogger<RoleService> logger)
    {
        _roleManager = roleManager;
        _userManager = userManager;
        _logger = logger;
    }

    public async Task<IReadOnlyList<RoleDto>> GetAllRolesAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Retrieving all roles from database.");

        var roles = await _roleManager.Roles
            .AsNoTracking()
            .Select(r => new RoleDto(r.Id, r.Name ?? string.Empty))
            .ToListAsync(cancellationToken);

        _logger.LogInformation("Retrieved {Count} roles.", roles.Count);
        return roles;
    }

    public async Task<UserRolesDto> GetUserRolesAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Retrieving roles for user '{UserId}'.", userId);

        var user = await _userManager.Users
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (user == null)
        {
            _logger.LogWarning("User '{UserId}' was not found.", userId);
            throw new NotFoundException(nameof(ApplicationUser), userId);
        }

        var roles = await _userManager.GetRolesAsync(user);

        return new UserRolesDto
        {
            UserId = user.Id,
            Email = user.Email,
            UserName = user.UserName,
            Roles = roles.ToList()
        };
    }

    public async Task AssignRoleAsync(Guid userId, string roleName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(roleName))
        {
            throw new BadRequestException("Role name cannot be empty.");
        }

        var normalizedRole = roleName.Trim();
        _logger.LogInformation("Attempting to assign role '{RoleName}' to user '{UserId}'.", normalizedRole, userId);

        var roleExists = await _roleManager.RoleExistsAsync(normalizedRole);
        if (!roleExists)
        {
            _logger.LogWarning("Role '{RoleName}' does not exist.", normalizedRole);
            throw new NotFoundException("Role", normalizedRole);
        }

        var user = await _userManager.Users
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (user == null)
        {
            _logger.LogWarning("User '{UserId}' was not found during role assignment.", userId);
            throw new NotFoundException(nameof(ApplicationUser), userId);
        }

        var isAlreadyInRole = await _userManager.IsInRoleAsync(user, normalizedRole);
        if (isAlreadyInRole)
        {
            _logger.LogWarning("User '{UserId}' is already assigned to role '{RoleName}'.", userId, normalizedRole);
            throw new ConflictException($"User '{user.UserName ?? userId.ToString()}' is already in role '{normalizedRole}'.");
        }

        // Enforce mutual exclusivity between Instructor and Student roles
        if (normalizedRole.Equals(AppRoles.Instructor, StringComparison.OrdinalIgnoreCase))
        {
            if (await _userManager.IsInRoleAsync(user, AppRoles.Student))
            {
                _logger.LogWarning("Cannot assign role '{RoleName}' to user '{UserId}' because they already hold the Student role.", normalizedRole, userId);
                throw new ConflictException("A user cannot be assigned both Instructor and Student roles simultaneously.");
            }
        }
        else if (normalizedRole.Equals(AppRoles.Student, StringComparison.OrdinalIgnoreCase))
        {
            if (await _userManager.IsInRoleAsync(user, AppRoles.Instructor))
            {
                _logger.LogWarning("Cannot assign role '{RoleName}' to user '{UserId}' because they already hold the Instructor role.", normalizedRole, userId);
                throw new ConflictException("A user cannot be assigned both Student and Instructor roles simultaneously.");
            }
        }

        var result = await _userManager.AddToRoleAsync(user, normalizedRole);
        if (!result.Succeeded)
        {
            var errors = string.Join("; ", result.Errors.Select(e => e.Description));
            _logger.LogError("Failed to assign role '{RoleName}' to user '{UserId}'. Errors: {Errors}", normalizedRole, userId, errors);
            throw new BadRequestException($"Failed to assign role: {errors}");
        }

        _logger.LogInformation("Role '{RoleName}' successfully assigned to user '{UserId}'.", normalizedRole, userId);
    }

    public async Task RemoveRoleAsync(Guid userId, string roleName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(roleName))
        {
            throw new BadRequestException("Role name cannot be empty.");
        }

        var normalizedRole = roleName.Trim();
        _logger.LogInformation("Attempting to remove role '{RoleName}' from user '{UserId}'.", normalizedRole, userId);

        var user = await _userManager.Users
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (user == null)
        {
            _logger.LogWarning("User '{UserId}' was not found during role removal.", userId);
            throw new NotFoundException(nameof(ApplicationUser), userId);
        }

        var isInRole = await _userManager.IsInRoleAsync(user, normalizedRole);
        if (!isInRole)
        {
            _logger.LogWarning("User '{UserId}' does not have role '{RoleName}'.", userId, normalizedRole);
            throw new BadRequestException($"User '{user.UserName ?? userId.ToString()}' does not have role '{normalizedRole}'.");
        }

        var result = await _userManager.RemoveFromRoleAsync(user, normalizedRole);
        if (!result.Succeeded)
        {
            var errors = string.Join("; ", result.Errors.Select(e => e.Description));
            _logger.LogError("Failed to remove role '{RoleName}' from user '{UserId}'. Errors: {Errors}", normalizedRole, userId, errors);
            throw new BadRequestException($"Failed to remove role: {errors}");
        }

        _logger.LogInformation("Role '{RoleName}' successfully removed from user '{UserId}'.", normalizedRole, userId);
    }

    public async Task<bool> RoleExistsAsync(string roleName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(roleName))
        {
            return false;
        }

        return await _roleManager.RoleExistsAsync(roleName.Trim());
    }

    public async Task<IReadOnlyList<PlatformUserDto>> GetPlatformUsersAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Retrieving platform users.");

        var users = await _userManager.Users
            .IgnoreQueryFilters()
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var result = new List<PlatformUserDto>();

        foreach (var user in users)
        {
            var roles = await _userManager.GetRolesAsync(user);
            if (roles.Contains(AppRoles.SuperAdmin))
            {
                result.Add(new PlatformUserDto
                {
                    Id = user.Id,
                    Name = $"{user.FirstName} {user.LastName}".Trim(),
                    Email = user.Email ?? string.Empty,
                    Role = "Platform Super Admin",
                    TwoFactor = user.TwoFactorEnabled,
                    LastLogin = user.UpdatedAt.ToString("yyyy-MM-dd HH:mm"),
                    Status = user.IsActive ? "Active" : "Inactive"
                });
            }
        }

        return result;
    }
}
