using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TrainHub.Application.Common.Models;
using TrainHub.Application.Features.CourseGroups.DTOs;
using TrainHub.Application.Features.Sessions.DTOs;
using TrainHub.Application.Features.Students.DTOs;
using TrainHub.Application.Interfaces;
using TrainHub.Domain.Constants;

namespace TrainHub.Api.Controllers;

/// <summary>
/// Manages Student profiles and records in the system.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class StudentsController : ControllerBase
{
    private readonly IStudentService _studentService;
    private readonly ILogger<StudentsController> _logger;

    public StudentsController(IStudentService studentService, ILogger<StudentsController> logger)
    {
        _studentService = studentService;
        _logger = logger;
    }

    /// <summary>
    /// Retrieves a paginated list of students with optional search, filtering, and sorting within the current organization.
    /// </summary>
    [HttpGet]
    [Authorize(Policy = AppPolicies.RequireCourseStaff)]
    [ProducesResponseType(typeof(PagedResult<StudentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<PagedResult<StudentDto>>> GetAll(
        [FromQuery] StudentQueryParameters parameters,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("HTTP GET api/students received.");
        var result = await _studentService.GetPagedAsync(parameters, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Retrieves a student by their unique ID.
    /// </summary>
    [HttpGet("{id:guid}")]
    [Authorize(Policy = AppPolicies.RequireOrgStaff)]
    [ProducesResponseType(typeof(StudentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<StudentDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        _logger.LogInformation("HTTP GET api/students/{Id} received.", id);
        var result = await _studentService.GetByIdAsync(id, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Creates a new student record for an existing user.
    /// </summary>
    [HttpPost]
    [Authorize(Policy = AppPolicies.RequireOrgStaff)]
    [ProducesResponseType(typeof(StudentDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<StudentDto>> Create([FromBody] CreateStudentDto dto, CancellationToken cancellationToken)
    {
        _logger.LogInformation("HTTP POST api/students received for user '{UserId}' with code '{StudentCode}'.",
            dto.UserId, dto.StudentCode);
        var result = await _studentService.CreateAsync(dto, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    /// <summary>
    /// Updates an existing student record.
    /// </summary>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = AppPolicies.RequireOrgStaff)]
    [ProducesResponseType(typeof(StudentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<StudentDto>> Update(Guid id, [FromBody] UpdateStudentDto dto, CancellationToken cancellationToken)
    {
        _logger.LogInformation("HTTP PUT api/students/{Id} received.", id);
        var result = await _studentService.UpdateAsync(id, dto, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Deletes a student if they have no active enrollments or attendance records.
    /// </summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Policy = AppPolicies.RequireOrgStaff)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        _logger.LogInformation("HTTP DELETE api/students/{Id} received.", id);
        await _studentService.DeleteAsync(id, cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Retrieves the profile of the currently authenticated student.
    /// </summary>
    [HttpGet("me")]
    [Authorize(Policy = AppPolicies.RequireStudent)]
    [ProducesResponseType(typeof(StudentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<StudentDto>> GetCurrentStudent(CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue)
        {
            _logger.LogWarning("HTTP GET api/students/me received but no valid user ID found in claims.");
            return Unauthorized();
        }

        _logger.LogInformation("HTTP GET api/students/me received for user '{UserId}'.", userId.Value);
        var result = await _studentService.GetByUserIdAsync(userId.Value, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Retrieves all courses the authenticated student is currently enrolled in.
    /// </summary>
    [HttpGet("me/courses")]
    [Authorize(Policy = AppPolicies.RequireStudent)]
    [ProducesResponseType(typeof(IEnumerable<StudentCourseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IEnumerable<StudentCourseDto>>> GetMyCourses(CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue)
        {
            _logger.LogWarning("HTTP GET api/students/me/courses received but no valid user ID found in claims.");
            return Unauthorized();
        }

        _logger.LogInformation("HTTP GET api/students/me/courses received for user '{UserId}'.", userId.Value);
        var result = await _studentService.GetMyCoursesAsync(userId.Value, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Retrieves all cohorts the authenticated student is currently enrolled in.
    /// </summary>
    [HttpGet("me/cohorts")]
    [Authorize(Policy = AppPolicies.RequireStudent)]
    [ProducesResponseType(typeof(IEnumerable<CourseGroupDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IEnumerable<CourseGroupDto>>> GetMyCohorts(CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue)
        {
            _logger.LogWarning("HTTP GET api/students/me/cohorts received but no valid user ID found in claims.");
            return Unauthorized();
        }

        _logger.LogInformation("HTTP GET api/students/me/cohorts received for user '{UserId}'.", userId.Value);
        var result = await _studentService.GetMyCohortsAsync(userId.Value, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Retrieves all scheduled sessions for cohorts the authenticated student is enrolled in.
    /// </summary>
    [HttpGet("me/sessions")]
    [Authorize(Policy = AppPolicies.RequireStudent)]
    [ProducesResponseType(typeof(IEnumerable<SessionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IEnumerable<SessionDto>>> GetMySessions(CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue)
        {
            _logger.LogWarning("HTTP GET api/students/me/sessions received but no valid user ID found in claims.");
            return Unauthorized();
        }

        _logger.LogInformation("HTTP GET api/students/me/sessions received for user '{UserId}'.", userId.Value);
        var result = await _studentService.GetMySessionsAsync(userId.Value, cancellationToken);
        return Ok(result);
    }

    private Guid? GetCurrentUserId()
    {
        var idClaim = User.FindFirstValue(ClaimTypes.NameIdentifier)
                      ?? User.FindFirstValue("sub");

        return Guid.TryParse(idClaim, out var userId) ? userId : null;
    }
}
