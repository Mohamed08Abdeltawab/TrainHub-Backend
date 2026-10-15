using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TrainHub.Application.Common.Models;
using TrainHub.Application.Features.Enrollments.DTOs;
using TrainHub.Application.Interfaces;
using TrainHub.Domain.Constants;

namespace TrainHub.Api.Controllers;

/// <summary>
/// Manages Student Enrollment entities and operations in the system.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class EnrollmentsController : ControllerBase
{
    private readonly IEnrollmentService _enrollmentService;
    private readonly ITrainHubDbContext _dbContext;
    private readonly ILogger<EnrollmentsController> _logger;

    public EnrollmentsController(
        IEnrollmentService enrollmentService,
        ITrainHubDbContext dbContext,
        ILogger<EnrollmentsController> logger)
    {
        _enrollmentService = enrollmentService;
        _dbContext = dbContext;
        _logger = logger;
    }

    /// <summary>
    /// Retrieves a paginated list of enrollments with optional filtering and sorting within the current organization.
    /// </summary>
    [HttpGet]
    [Authorize(Policy = AppPolicies.RequireCourseStaff)]
    [ProducesResponseType(typeof(PagedResult<EnrollmentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<PagedResult<EnrollmentDto>>> GetAll(
        [FromQuery] EnrollmentQueryParameters parameters,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("HTTP GET api/enrollments received.");
        var result = await _enrollmentService.GetPagedAsync(parameters, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Retrieves an enrollment by its unique ID.
    /// Authorized for OrgStaff, assigned Instructor, or the enrolled Student themselves.
    /// </summary>
    [HttpGet("{id:guid}")]
    [Authorize]
    [ProducesResponseType(typeof(EnrollmentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EnrollmentDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        _logger.LogInformation("HTTP GET api/enrollments/{Id} received.", id);
        var result = await _enrollmentService.GetByIdAsync(id, cancellationToken);

        var isOrgStaff = User.IsInRole(AppRoles.SuperAdmin) || User.IsInRole(AppRoles.OrgAdmin);
        if (!isOrgStaff)
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue)
            {
                return Unauthorized();
            }

            if (User.IsInRole(AppRoles.Student))
            {
                var callerStudent = await _dbContext.Students
                    .AsNoTracking()
                    .FirstOrDefaultAsync(s => s.UserId == userId.Value, cancellationToken);

                if (callerStudent == null || callerStudent.Id != result.StudentId)
                {
                    return Forbid();
                }
            }
            else if (User.IsInRole(AppRoles.Instructor))
            {
                var instructor = await _dbContext.Instructors
                    .AsNoTracking()
                    .FirstOrDefaultAsync(i => i.UserId == userId.Value, cancellationToken);

                var group = await _dbContext.CourseGroups
                    .AsNoTracking()
                    .FirstOrDefaultAsync(cg => cg.Id == result.CourseGroupId, cancellationToken);

                if (instructor == null || group == null || group.InstructorId != instructor.Id)
                {
                    return Forbid();
                }
            }
            else
            {
                return Forbid();
            }
        }

        return Ok(result);
    }

    /// <summary>
    /// Enrolls a student into a course group in the current organization.
    /// Authorized for OrgStaff (SuperAdmin, OrgAdmin) and Students enrolling themselves.
    /// </summary>
    [HttpPost]
    [Authorize]
    [ProducesResponseType(typeof(EnrollmentDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<EnrollmentDto>> Create(
        [FromBody] CreateEnrollmentDto dto,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("HTTP POST api/enrollments received for student '{StudentId}' and group '{CourseGroupId}'.",
            dto.StudentId, dto.CourseGroupId);

        var isOrgStaff = User.IsInRole(AppRoles.SuperAdmin) || User.IsInRole(AppRoles.OrgAdmin);
        if (!isOrgStaff)
        {
            if (User.IsInRole(AppRoles.Student))
            {
                var userId = GetCurrentUserId();
                if (!userId.HasValue)
                {
                    return Unauthorized();
                }

                var callerStudent = await _dbContext.Students
                    .AsNoTracking()
                    .FirstOrDefaultAsync(s => s.UserId == userId.Value, cancellationToken);

                if (callerStudent == null || callerStudent.Id != dto.StudentId)
                {
                    _logger.LogWarning("Forbidden: Student user '{CallerUserId}' attempted to enroll student '{TargetStudentId}'.",
                        userId.Value, dto.StudentId);
                    return Forbid();
                }
            }
            else
            {
                return Forbid();
            }
        }

        var result = await _enrollmentService.CreateAsync(dto, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    /// <summary>
    /// Updates details of an existing enrollment record.
    /// </summary>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = AppPolicies.RequireOrgStaff)]
    [ProducesResponseType(typeof(EnrollmentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<EnrollmentDto>> Update(
        Guid id,
        [FromBody] UpdateEnrollmentDto dto,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("HTTP PUT api/enrollments/{Id} received.", id);
        var result = await _enrollmentService.UpdateAsync(id, dto, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Updates the lifecycle status of an existing enrollment.
    /// </summary>
    [HttpPatch("{id:guid}/status")]
    [Authorize(Policy = AppPolicies.RequireOrgStaff)]
    [ProducesResponseType(typeof(EnrollmentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<EnrollmentDto>> UpdateStatus(
        Guid id,
        [FromBody] UpdateEnrollmentStatusDto dto,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("HTTP PATCH api/enrollments/{Id}/status received with status '{Status}'.", id, dto.Status);
        var result = await _enrollmentService.UpdateStatusAsync(id, dto, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Deletes an enrollment record if no dependent attendance records exist.
    /// </summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Policy = AppPolicies.RequireOrgStaff)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        _logger.LogInformation("HTTP DELETE api/enrollments/{Id} received.", id);
        await _enrollmentService.DeleteAsync(id, cancellationToken);
        return NoContent();
    }

    private Guid? GetCurrentUserId()
    {
        var idClaim = User?.FindFirstValue(ClaimTypes.NameIdentifier)
                      ?? User?.FindFirstValue("sub");

        return Guid.TryParse(idClaim, out var userId) ? userId : null;
    }
}
