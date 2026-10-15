using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TrainHub.Application.Common.Models;
using TrainHub.Application.Features.Attendances.DTOs;
using TrainHub.Application.Interfaces;
using TrainHub.Domain.Constants;

namespace TrainHub.Api.Controllers;

/// <summary>
/// Manages AttendanceRecord entities and operations in the system.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AttendancesController : ControllerBase
{
    private readonly IAttendanceService _attendanceService;
    private readonly ILogger<AttendancesController> _logger;

    public AttendancesController(
        IAttendanceService attendanceService,
        ILogger<AttendancesController> logger)
    {
        _attendanceService = attendanceService;
        _logger = logger;
    }

    /// <summary>
    /// Retrieves a paginated list of attendance records with optional filtering and sorting within the current organization.
    /// Students can only view their own attendance records.
    /// Instructors can only view records for sessions/course groups they teach.
    /// </summary>
    [HttpGet]
    [Authorize]
    [ProducesResponseType(typeof(PagedResult<AttendanceDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<PagedResult<AttendanceDto>>> GetAll(
        [FromQuery] AttendanceQueryParameters parameters,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("HTTP GET api/attendances received.");
        var userId = GetCurrentUserId();
        var result = await _attendanceService.GetPagedAsync(parameters, userId, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Retrieves an attendance record by its unique ID.
    /// Students can only view their own attendance records.
    /// Instructors can only view records for sessions/course groups they teach.
    /// </summary>
    [HttpGet("{id:guid}")]
    [Authorize]
    [ProducesResponseType(typeof(AttendanceDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AttendanceDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        _logger.LogInformation("HTTP GET api/attendances/{Id} received.", id);
        var userId = GetCurrentUserId();
        var result = await _attendanceService.GetByIdAsync(id, userId, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Retrieves all attendance records for a specific session within the current organization.
    /// Restricted to CourseStaff (OrgAdmin, SuperAdmin, and the assigned Instructor).
    /// </summary>
    [HttpGet("session/{sessionId:guid}")]
    [HttpGet("/api/sessions/{sessionId:guid}/attendance")]
    [Authorize(Policy = AppPolicies.RequireCourseStaff)]
    [ProducesResponseType(typeof(IReadOnlyList<AttendanceDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<AttendanceDto>>> GetBySessionId(
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("HTTP GET api/attendances/session/{SessionId} received.", sessionId);
        var userId = GetCurrentUserId();
        var result = await _attendanceService.GetBySessionIdAsync(sessionId, userId, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Calculates the attendance percentage and breakdown for a student, optionally filtered by course group.
    /// Students can only view their own percentage.
    /// Instructors can only view percentages for students in course groups they teach.
    /// </summary>
    [HttpGet("student/{studentId:guid}/percentage")]
    [HttpGet("/api/students/{studentId:guid}/attendance/percentage")]
    [Authorize]
    [ProducesResponseType(typeof(AttendancePercentageDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AttendancePercentageDto>> GetPercentage(
        Guid studentId,
        [FromQuery] Guid? courseGroupId,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("HTTP GET api/attendances/student/{StudentId}/percentage received.", studentId);
        var userId = GetCurrentUserId();
        var result = await _attendanceService.GetPercentageAsync(studentId, courseGroupId, userId, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Records attendance for a student in a session.
    /// Authorized for OrgStaff (SuperAdmin, OrgAdmin) and Instructors assigned to the session or course group.
    /// </summary>
    [HttpPost]
    [Authorize(Policy = AppPolicies.RequireCourseStaff)]
    [ProducesResponseType(typeof(AttendanceDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<AttendanceDto>> Create(
        [FromBody] CreateAttendanceDto dto,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("HTTP POST api/attendances received for student '{StudentId}' and session '{SessionId}'.",
            dto.StudentId, dto.SessionId);
        var userId = GetCurrentUserId();
        var result = await _attendanceService.CreateAsync(dto, userId, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    /// <summary>
    /// Saves attendance records for multiple students in a single session as an atomic batch.
    /// Authorized for OrgStaff (SuperAdmin, OrgAdmin) and Instructors assigned to the session or course group.
    /// </summary>
    [HttpPost("/api/sessions/{sessionId:guid}/attendance/batch")]
    [HttpPost("session/{sessionId:guid}/batch")]
    [Authorize(Policy = AppPolicies.RequireCourseStaff)]
    [ProducesResponseType(typeof(IReadOnlyList<AttendanceDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<IReadOnlyList<AttendanceDto>>> SaveBatch(
        Guid sessionId,
        [FromBody] BatchAttendanceRequestDto dto,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("HTTP POST api/sessions/{SessionId}/attendance/batch received.", sessionId);
        var userId = GetCurrentUserId();
        var result = await _attendanceService.SaveBatchAsync(sessionId, dto, userId, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Updates details (status and notes) of an existing attendance record.
    /// Authorized for OrgStaff and assigned Instructors.
    /// </summary>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = AppPolicies.RequireCourseStaff)]
    [ProducesResponseType(typeof(AttendanceDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<AttendanceDto>> Update(
        Guid id,
        [FromBody] UpdateAttendanceDto dto,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("HTTP PUT api/attendances/{Id} received.", id);
        var userId = GetCurrentUserId();
        var result = await _attendanceService.UpdateAsync(id, dto, userId, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Updates the status of an existing attendance record.
    /// Authorized for OrgStaff and assigned Instructors.
    /// </summary>
    [HttpPatch("{id:guid}/status")]
    [Authorize(Policy = AppPolicies.RequireCourseStaff)]
    [ProducesResponseType(typeof(AttendanceDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<AttendanceDto>> UpdateStatus(
        Guid id,
        [FromBody] UpdateAttendanceStatusDto dto,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("HTTP PATCH api/attendances/{Id}/status received with status '{Status}'.", id, dto.Status);
        var userId = GetCurrentUserId();
        var result = await _attendanceService.UpdateStatusAsync(id, dto, userId, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Deletes an attendance record.
    /// Authorized for OrgStaff and assigned Instructors.
    /// </summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Policy = AppPolicies.RequireCourseStaff)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        _logger.LogInformation("HTTP DELETE api/attendances/{Id} received.", id);
        var userId = GetCurrentUserId();
        await _attendanceService.DeleteAsync(id, userId, cancellationToken);
        return NoContent();
    }

    private Guid? GetCurrentUserId()
    {
        var idClaim = User?.FindFirstValue(ClaimTypes.NameIdentifier)
                      ?? User?.FindFirstValue("sub");

        return Guid.TryParse(idClaim, out var userId) ? userId : null;
    }
}
