using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TrainHub.Application.Features.Organizations.DTOs;
using TrainHub.Application.Interfaces;
using TrainHub.Domain.Constants;

namespace TrainHub.Api.Controllers;

/// <summary>
/// Manages Organizations in the system.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class OrganizationsController : ControllerBase
{
    private readonly IOrganizationService _organizationService;
    private readonly ICurrentOrganizationService _currentOrganizationService;
    private readonly ILogger<OrganizationsController> _logger;

    public OrganizationsController(
        IOrganizationService organizationService,
        ICurrentOrganizationService currentOrganizationService,
        ILogger<OrganizationsController> logger)
    {
        _organizationService = organizationService;
        _currentOrganizationService = currentOrganizationService;
        _logger = logger;
    }

    /// <summary>
    /// Retrieves the current user's organization profile.
    /// Accessible to SuperAdmin and OrgAdmin.
    /// </summary>
    [HttpGet("current")]
    [HttpGet("me")]
    [Authorize(Policy = AppPolicies.RequireOrgStaff)]
    [ProducesResponseType(typeof(OrganizationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrganizationDto>> GetCurrent(CancellationToken cancellationToken)
    {
        var orgId = _currentOrganizationService.OrganizationId;
        if (!orgId.HasValue || orgId.Value == Guid.Empty)
        {
            _logger.LogWarning("GetCurrent organization failed: No organization context available for caller.");
            return BadRequest(new ProblemDetails
            {
                Title = "Missing Organization Context",
                Detail = "Current user does not have an active organization context.",
                Status = StatusCodes.Status400BadRequest
            });
        }

        _logger.LogInformation("HTTP GET api/organizations/current received for org '{OrganizationId}'.", orgId.Value);
        var result = await _organizationService.GetByIdAsync(orgId.Value, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Updates the current user's organization profile.
    /// Accessible to SuperAdmin and OrgAdmin.
    /// </summary>
    [HttpPut("current")]
    [HttpPut("me")]
    [Authorize(Policy = AppPolicies.RequireOrgStaff)]
    [ProducesResponseType(typeof(OrganizationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<OrganizationDto>> UpdateCurrent([FromBody] UpdateOrganizationProfileDto dto, CancellationToken cancellationToken)
    {
        var orgId = _currentOrganizationService.OrganizationId;
        if (!orgId.HasValue || orgId.Value == Guid.Empty)
        {
            _logger.LogWarning("UpdateCurrent organization failed: No organization context available for caller.");
            return BadRequest(new ProblemDetails
            {
                Title = "Missing Organization Context",
                Detail = "Current user does not have an active organization context.",
                Status = StatusCodes.Status400BadRequest
            });
        }

        _logger.LogInformation("HTTP PUT api/organizations/current received for org '{OrganizationId}'.", orgId.Value);
        var result = await _organizationService.UpdateProfileAsync(orgId.Value, dto, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Retrieves all organizations in the platform.
    /// Strictly restricted to SuperAdmin.
    /// </summary>
    [HttpGet]
    [Authorize(Policy = AppPolicies.RequireSuperAdmin)]
    [ProducesResponseType(typeof(IEnumerable<OrganizationDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<OrganizationDto>>> GetAll(CancellationToken cancellationToken)
    {
        _logger.LogInformation("HTTP GET api/organizations received.");
        var result = await _organizationService.GetAllAsync(cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Retrieves an organization by its unique ID.
    /// SuperAdmin can view any; OrgAdmin can only view their own organization.
    /// </summary>
    [HttpGet("{id:guid}")]
    [Authorize(Policy = AppPolicies.RequireOrgStaff)]
    [ProducesResponseType(typeof(OrganizationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrganizationDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        _logger.LogInformation("HTTP GET api/organizations/{Id} received.", id);

        if (!User.IsInRole(AppRoles.SuperAdmin))
        {
            var callerOrgId = _currentOrganizationService.OrganizationId;
            if (!callerOrgId.HasValue || callerOrgId.Value != id)
            {
                _logger.LogWarning("Forbidden access: User belonging to org '{CallerOrgId}' attempted to access org '{TargetOrgId}'.", callerOrgId, id);
                return Forbid();
            }
        }

        var result = await _organizationService.GetByIdAsync(id, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Creates a new organization with validation for unique slug, plans, and statuses.
    /// Strictly restricted to SuperAdmin.
    /// </summary>
    [HttpPost]
    [Authorize(Policy = AppPolicies.RequireSuperAdmin)]
    [ProducesResponseType(typeof(OrganizationDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<OrganizationDto>> Create([FromBody] CreateOrganizationDto dto, CancellationToken cancellationToken)
    {
        _logger.LogInformation("HTTP POST api/organizations received for '{Name}'.", dto.Name);
        var result = await _organizationService.CreateAsync(dto, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    /// <summary>
    /// Updates an existing organization with conflict and data validation checks.
    /// SuperAdmin can update any organization; OrgAdmin can only update their own organization.
    /// </summary>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = AppPolicies.RequireOrgStaff)]
    [ProducesResponseType(typeof(OrganizationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<OrganizationDto>> Update(Guid id, [FromBody] UpdateOrganizationDto dto, CancellationToken cancellationToken)
    {
        _logger.LogInformation("HTTP PUT api/organizations/{Id} received.", id);

        if (!User.IsInRole(AppRoles.SuperAdmin))
        {
            var callerOrgId = _currentOrganizationService.OrganizationId;
            if (!callerOrgId.HasValue || callerOrgId.Value != id)
            {
                _logger.LogWarning("Forbidden access: User belonging to org '{CallerOrgId}' attempted to update org '{TargetOrgId}'.", callerOrgId, id);
                return Forbid();
            }
        }

        var result = await _organizationService.UpdateAsync(id, dto, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Deletes an organization if it has no active dependent resources.
    /// Strictly restricted to SuperAdmin.
    /// </summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Policy = AppPolicies.RequireSuperAdmin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        _logger.LogInformation("HTTP DELETE api/organizations/{Id} received.", id);
        await _organizationService.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}
