using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TrainHub.Application.Common.Models;
using TrainHub.Application.Features.CourseGroups.DTOs;
using TrainHub.Application.Interfaces;
using TrainHub.Domain.Constants;

namespace TrainHub.Api.Controllers;

/// <summary>
/// Manages CourseGroup entities (cohorts/batches) in the system.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CourseGroupsController : ControllerBase
{
    private readonly ICourseGroupService _courseGroupService;
    private readonly ILogger<CourseGroupsController> _logger;

    public CourseGroupsController(
        ICourseGroupService courseGroupService,
        ILogger<CourseGroupsController> logger)
    {
        _courseGroupService = courseGroupService;
        _logger = logger;
    }

    /// <summary>
    /// Retrieves a paginated list of course groups with optional search, filtering, and sorting within the current organization.
    /// </summary>
    [HttpGet]
    [Authorize(Policy = AppPolicies.RequireCourseStaff)]
    [ProducesResponseType(typeof(PagedResult<CourseGroupDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<PagedResult<CourseGroupDto>>> GetAll(
        [FromQuery] CourseGroupQueryParameters parameters,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("HTTP GET api/coursegroups received.");
        var result = await _courseGroupService.GetPagedAsync(parameters, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Retrieves a course group by its unique ID.
    /// </summary>
    [HttpGet("{id:guid}")]
    [Authorize(Policy = AppPolicies.RequireCourseStaff)]
    [ProducesResponseType(typeof(CourseGroupDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CourseGroupDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        _logger.LogInformation("HTTP GET api/coursegroups/{Id} received.", id);
        var result = await _courseGroupService.GetByIdAsync(id, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Creates a new course group in the organization.
    /// </summary>
    [HttpPost]
    [Authorize(Policy = AppPolicies.RequireOrgStaff)]
    [ProducesResponseType(typeof(CourseGroupDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<CourseGroupDto>> Create(
        [FromBody] CreateCourseGroupDto dto,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("HTTP POST api/coursegroups received with name '{GroupName}'.", dto.Name);
        var result = await _courseGroupService.CreateAsync(dto, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    /// <summary>
    /// Updates an existing course group record.
    /// </summary>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = AppPolicies.RequireOrgStaff)]
    [ProducesResponseType(typeof(CourseGroupDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<CourseGroupDto>> Update(
        Guid id,
        [FromBody] UpdateCourseGroupDto dto,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("HTTP PUT api/coursegroups/{Id} received.", id);
        var result = await _courseGroupService.UpdateAsync(id, dto, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Deletes a course group if it has no associated enrollments or sessions.
    /// </summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Policy = AppPolicies.RequireOrgStaff)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        _logger.LogInformation("HTTP DELETE api/coursegroups/{Id} received.", id);
        await _courseGroupService.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}
