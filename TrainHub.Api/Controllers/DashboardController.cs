using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TrainHub.Application.Features.Dashboard.DTOs;
using TrainHub.Application.Interfaces;
using TrainHub.Domain.Constants;

namespace TrainHub.Api.Controllers;

/// <summary>
/// Provides dashboard metrics and statistics for training center management.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class DashboardController : ControllerBase
{
    private readonly IDashboardService _dashboardService;
    private readonly ILogger<DashboardController> _logger;

    public DashboardController(IDashboardService dashboardService, ILogger<DashboardController> logger)
    {
        _dashboardService = dashboardService;
        _logger = logger;
    }

    /// <summary>
    /// Role-aware dashboard payload. Authorization is resolved from JWT claims.
    /// </summary>
    [HttpGet("content")]
    [Authorize]
    [ProducesResponseType(typeof(DashboardContentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<DashboardContentDto>> GetDashboardContent(CancellationToken cancellationToken)
    {
        _logger.LogInformation("HTTP GET api/dashboard/content received.");
        var result = await _dashboardService.GetDashboardContentAsync(cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Retrieves a comprehensive dashboard summary containing student, course, instructor, and attendance statistics for the current organization.
    /// </summary>
    [HttpGet]
    [Authorize(Policy = AppPolicies.RequireOrgStaff)]
    [ProducesResponseType(typeof(DashboardSummaryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<DashboardSummaryDto>> GetSummary(CancellationToken cancellationToken)
    {
        _logger.LogInformation("HTTP GET api/dashboard received.");
        var result = await _dashboardService.GetDashboardSummaryAsync(cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Retrieves student metrics including total, active, inactive, and enrollment breakdowns for the current organization.
    /// </summary>
    [HttpGet("students")]
    [Authorize(Policy = AppPolicies.RequireOrgStaff)]
    [ProducesResponseType(typeof(StudentStatisticsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<StudentStatisticsDto>> GetStudentStatistics(CancellationToken cancellationToken)
    {
        _logger.LogInformation("HTTP GET api/dashboard/students received.");
        var result = await _dashboardService.GetStudentStatisticsAsync(cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Retrieves course metrics including status breakdowns for courses and cohorts for the current organization.
    /// </summary>
    [HttpGet("courses")]
    [Authorize(Policy = AppPolicies.RequireOrgStaff)]
    [ProducesResponseType(typeof(CourseStatisticsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<CourseStatisticsDto>> GetCourseStatistics(CancellationToken cancellationToken)
    {
        _logger.LogInformation("HTTP GET api/dashboard/courses received.");
        var result = await _dashboardService.GetCourseStatisticsAsync(cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Retrieves instructor metrics including active, inactive, and teaching assignment metrics for the current organization.
    /// </summary>
    [HttpGet("instructors")]
    [Authorize(Policy = AppPolicies.RequireOrgStaff)]
    [ProducesResponseType(typeof(InstructorStatisticsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<InstructorStatisticsDto>> GetInstructorStatistics(CancellationToken cancellationToken)
    {
        _logger.LogInformation("HTTP GET api/dashboard/instructors received.");
        var result = await _dashboardService.GetInstructorStatisticsAsync(cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Retrieves attendance and session metrics, optionally filtered by course group, for the current organization.
    /// </summary>
    [HttpGet("attendance")]
    [Authorize(Policy = AppPolicies.RequireOrgStaff)]
    [ProducesResponseType(typeof(AttendanceStatisticsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AttendanceStatisticsDto>> GetAttendanceStatistics(
        [FromQuery] Guid? courseGroupId,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("HTTP GET api/dashboard/attendance received (CourseGroupId: {CourseGroupId}).", courseGroupId);
        var result = await _dashboardService.GetAttendanceStatisticsAsync(courseGroupId, cancellationToken);
        return Ok(result);
    }
}
