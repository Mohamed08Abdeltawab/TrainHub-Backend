using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TrainHub.Application.Common.Models;
using TrainHub.Application.Features.Assignments.DTOs;
using TrainHub.Application.Interfaces;
using TrainHub.Domain.Constants;

namespace TrainHub.Api.Controllers;

/// <summary>
/// Manages Assignment entities and operations for course groups.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AssignmentsController : ControllerBase
{
    private readonly IAssignmentService _assignmentService;
    private readonly ILogger<AssignmentsController> _logger;

    public AssignmentsController(
        IAssignmentService assignmentService,
        ILogger<AssignmentsController> logger)
    {
        _assignmentService = assignmentService;
        _logger = logger;
    }

    /// <summary>
    /// Retrieves a paginated list of assignments within the current organization.
    /// Instructors only see assignments for course groups they teach.
    /// </summary>
    [HttpGet]
    [Authorize(Policy = AppPolicies.RequireCourseStaff)]
    [ProducesResponseType(typeof(PagedResult<AssignmentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<PagedResult<AssignmentDto>>> GetAll(
        [FromQuery] AssignmentQueryParameters parameters,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("HTTP GET api/assignments received.");
        var userId = GetCurrentUserId();
        var result = await _assignmentService.GetPagedAsync(parameters, userId, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Retrieves an assignment by its unique ID.
    /// </summary>
    [HttpGet("{id:guid}")]
    [Authorize(Policy = AppPolicies.RequireCourseStaff)]
    [ProducesResponseType(typeof(AssignmentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AssignmentDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        _logger.LogInformation("HTTP GET api/assignments/{Id} received.", id);
        var userId = GetCurrentUserId();
        var result = await _assignmentService.GetByIdAsync(id, userId, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Creates a new assignment for a course group.
    /// Authorized for OrgStaff (SuperAdmin, OrgAdmin) and Instructors assigned to the course group.
    /// </summary>
    [HttpPost]
    [Authorize(Policy = AppPolicies.RequireCourseStaff)]
    [ProducesResponseType(typeof(AssignmentDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<AssignmentDto>> Create(
        [FromBody] CreateAssignmentDto dto,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("HTTP POST api/assignments received for course group '{CourseGroupId}'.", dto.CourseGroupId);
        var userId = GetCurrentUserId();
        var result = await _assignmentService.CreateAsync(dto, userId, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    /// <summary>
    /// Updates an existing assignment.
    /// Authorized for OrgStaff and the assigned Instructor.
    /// </summary>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = AppPolicies.RequireCourseStaff)]
    [ProducesResponseType(typeof(AssignmentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<AssignmentDto>> Update(
        Guid id,
        [FromBody] UpdateAssignmentDto dto,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("HTTP PUT api/assignments/{Id} received.", id);
        var userId = GetCurrentUserId();
        var result = await _assignmentService.UpdateAsync(id, dto, userId, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Deletes an assignment record.
    /// Authorized for OrgStaff and the assigned Instructor.
    /// </summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Policy = AppPolicies.RequireCourseStaff)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        _logger.LogInformation("HTTP DELETE api/assignments/{Id} received.", id);
        var userId = GetCurrentUserId();
        await _assignmentService.DeleteAsync(id, userId, cancellationToken);
        return NoContent();
    }

    private Guid? GetCurrentUserId()
    {
        var idClaim = User?.FindFirstValue(ClaimTypes.NameIdentifier)
                      ?? User?.FindFirstValue("sub");

        return Guid.TryParse(idClaim, out var userId) ? userId : null;
    }
}
