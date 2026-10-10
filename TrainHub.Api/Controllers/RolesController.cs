using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TrainHub.Application.Features.Roles.DTOs;
using TrainHub.Application.Interfaces;
using TrainHub.Domain.Constants;

namespace TrainHub.Api.Controllers;

/// <summary>
/// Manages system roles and user role assignments. Restricted to platform SuperAdmin.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = AppPolicies.RequireSuperAdmin)]
public class RolesController : ControllerBase
{
    private readonly IRoleService _roleService;
    private readonly ILogger<RolesController> _logger;

    public RolesController(IRoleService roleService, ILogger<RolesController> logger)
    {
        _roleService = roleService;
        _logger = logger;
    }

    /// <summary>
    /// Gets all roles defined in the system.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<RoleDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<RoleDto>>> GetAllRoles(CancellationToken cancellationToken)
    {
        _logger.LogInformation("HTTP GET api/roles received.");
        var roles = await _roleService.GetAllRolesAsync(cancellationToken);
        return Ok(roles);
    }

    /// <summary>
    /// Gets all roles assigned to a specific user.
    /// </summary>
    [HttpGet("user/{userId:guid}")]
    [ProducesResponseType(typeof(UserRolesDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserRolesDto>> GetUserRoles(Guid userId, CancellationToken cancellationToken)
    {
        _logger.LogInformation("HTTP GET api/roles/user/{UserId} received.", userId);
        var userRoles = await _roleService.GetUserRolesAsync(userId, cancellationToken);
        return Ok(userRoles);
    }

    /// <summary>
    /// Assigns a role to a user.
    /// </summary>
    [HttpPost("assign")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AssignRole([FromBody] AssignRoleRequest request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("HTTP POST api/roles/assign received for user {UserId}, role {RoleName}.", request.UserId, request.RoleName);
        await _roleService.AssignRoleAsync(request.UserId, request.RoleName, cancellationToken);
        return Ok(new { message = $"Role '{request.RoleName}' assigned successfully to user '{request.UserId}'." });
    }

    /// <summary>
    /// Removes a role from a user.
    /// </summary>
    [HttpPost("remove")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveRole([FromBody] RemoveRoleRequest request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("HTTP POST api/roles/remove received for user {UserId}, role {RoleName}.", request.UserId, request.RoleName);
        await _roleService.RemoveRoleAsync(request.UserId, request.RoleName, cancellationToken);
        return Ok(new { message = $"Role '{request.RoleName}' removed successfully from user '{request.UserId}'." });
    }
}
