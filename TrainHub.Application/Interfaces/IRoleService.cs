using TrainHub.Application.Features.Roles.DTOs;

namespace TrainHub.Application.Interfaces;

/// <summary>
/// Service interface for querying and managing application roles and user assignments.
/// </summary>
public interface IRoleService
{
    /// <summary>
    /// Retrieves all roles configured in the system.
    /// </summary>
    Task<IReadOnlyList<RoleDto>> GetAllRolesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the roles assigned to a specific user.
    /// </summary>
    Task<UserRolesDto> GetUserRolesAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Assigns a role to a user.
    /// </summary>
    Task AssignRoleAsync(Guid userId, string roleName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a role from a user.
    /// </summary>
    Task RemoveRoleAsync(Guid userId, string roleName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks whether a given role exists.
    /// </summary>
    Task<bool> RoleExistsAsync(string roleName, CancellationToken cancellationToken = default);
}
