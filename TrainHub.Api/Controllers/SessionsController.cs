using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TrainHub.Application.Common.Models;
using TrainHub.Application.Features.Sessions.DTOs;
using TrainHub.Application.Interfaces;
using TrainHub.Domain.Constants;

namespace TrainHub.Api.Controllers;

/// <summary>
/// Manages Session entities and operations in the system.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class SessionsController : ControllerBase
{
    private readonly ISessionService _sessionService;
    private readonly ILogger<SessionsController> _logger;

    public SessionsController(
        ISessionService sessionService,
        ILogger<SessionsController> logger)
    {
        _sessionService = sessionService;
        _logger = logger;
    }

    /// <summary>
    /// Retrieves a paginated list of sessions with optional filtering and sorting within the current organization.
    /// </summary>
    [HttpGet]
    [Authorize]
    [ProducesResponseType(typeof(PagedResult<SessionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<PagedResult<SessionDto>>> GetAll(
        [FromQuery] SessionQueryParameters parameters,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("HTTP GET api/sessions received.");
        var result = await _sessionService.GetPagedAsync(parameters, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Retrieves a session by its unique ID.
    /// </summary>
    [HttpGet("{id:guid}")]
    [Authorize]
    [ProducesResponseType(typeof(SessionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SessionDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        _logger.LogInformation("HTTP GET api/sessions/{Id} received.", id);
        var result = await _sessionService.GetByIdAsync(id, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Retrieves all sessions for a specific course group within the current organization.
    /// </summary>
    [HttpGet("group/{courseGroupId:guid}")]
    [HttpGet("/api/coursegroups/{courseGroupId:guid}/sessions")]
    [Authorize]
    [ProducesResponseType(typeof(IReadOnlyList<SessionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<SessionDto>>> GetByCourseGroupId(
        Guid courseGroupId,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("HTTP GET api/sessions/group/{CourseGroupId} received.", courseGroupId);
        var result = await _sessionService.GetByCourseGroupIdAsync(courseGroupId, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Creates a new session in the current organization.
    /// Authorized for OrgStaff (SuperAdmin, OrgAdmin) and Instructors assigned to the course group.
    /// </summary>
    [HttpPost]
    [Authorize(Policy = AppPolicies.RequireCourseStaff)]
    [ProducesResponseType(typeof(SessionDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<SessionDto>> Create(
        [FromBody] CreateSessionDto dto,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("HTTP POST api/sessions received for course group '{CourseGroupId}'.", dto.CourseGroupId);
        var userId = GetCurrentUserId();
        var result = await _sessionService.CreateAsync(dto, userId, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    /// <summary>
    /// Updates details of an existing session record.
    /// Authorized for OrgStaff and the assigned Instructor.
    /// </summary>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = AppPolicies.RequireCourseStaff)]
    [ProducesResponseType(typeof(SessionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<SessionDto>> Update(
        Guid id,
        [FromBody] UpdateSessionDto dto,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("HTTP PUT api/sessions/{Id} received.", id);
        var userId = GetCurrentUserId();
        var result = await _sessionService.UpdateAsync(id, dto, userId, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Reschedules an existing session to new start and end times.
    /// Authorized for OrgStaff and the assigned Instructor.
    /// </summary>
    [HttpPatch("{id:guid}/reschedule")]
    [HttpPut("{id:guid}/reschedule")]
    [Authorize(Policy = AppPolicies.RequireCourseStaff)]
    [ProducesResponseType(typeof(SessionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<SessionDto>> Reschedule(
        Guid id,
        [FromBody] RescheduleSessionDto dto,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("HTTP PATCH/PUT api/sessions/{Id}/reschedule received.", id);
        var userId = GetCurrentUserId();
        var result = await _sessionService.RescheduleAsync(id, dto, userId, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Updates the lifecycle status of an existing session.
    /// Authorized for OrgStaff and the assigned Instructor.
    /// </summary>
    [HttpPatch("{id:guid}/status")]
    [Authorize(Policy = AppPolicies.RequireCourseStaff)]
    [ProducesResponseType(typeof(SessionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<SessionDto>> UpdateStatus(
        Guid id,
        [FromBody] UpdateSessionStatusDto dto,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("HTTP PATCH api/sessions/{Id}/status received with status '{Status}'.", id, dto.Status);
        var userId = GetCurrentUserId();
        var result = await _sessionService.UpdateStatusAsync(id, dto, userId, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Deletes a session record if no dependent attendance records exist.
    /// Authorized for OrgStaff and the assigned Instructor.
    /// </summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Policy = AppPolicies.RequireCourseStaff)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        _logger.LogInformation("HTTP DELETE api/sessions/{Id} received.", id);
        var userId = GetCurrentUserId();
        await _sessionService.DeleteAsync(id, userId, cancellationToken);
        return NoContent();
    }

    private Guid? GetCurrentUserId()
    {
        var idClaim = User?.FindFirstValue(ClaimTypes.NameIdentifier)
                      ?? User?.FindFirstValue("sub");

        return Guid.TryParse(idClaim, out var userId) ? userId : null;
    }
}
