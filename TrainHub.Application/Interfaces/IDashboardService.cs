using TrainHub.Application.Features.Dashboard.DTOs;

namespace TrainHub.Application.Interfaces;

/// <summary>
/// Service contract for querying dashboard statistics across students, courses, instructors, and attendance.
/// All queries are tenant-scoped to the current organization context.
/// </summary>
public interface IDashboardService
{
    /// <summary>
    /// Retrieves a comprehensive dashboard summary containing all student, course, instructor, and attendance statistics for the current organization.
    /// </summary>
    Task<DashboardSummaryDto> GetDashboardSummaryAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves student statistics including total, active, inactive students and enrollment metrics for the current organization.
    /// </summary>
    Task<StudentStatisticsDto> GetStudentStatisticsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves course statistics including status breakdowns for courses and course groups (cohorts) for the current organization.
    /// </summary>
    Task<CourseStatisticsDto> GetCourseStatisticsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves instructor statistics including active, inactive, and assignment metrics for the current organization.
    /// </summary>
    Task<InstructorStatisticsDto> GetInstructorStatisticsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves attendance and session statistics, optionally filtered by course group, for the current organization.
    /// </summary>
    Task<AttendanceStatisticsDto> GetAttendanceStatisticsAsync(Guid? courseGroupId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves role-aware dashboard content matching the React SPA presentation model.
    /// Role and identity are resolved securely from caller JWT claims.
    /// </summary>
    Task<DashboardContentDto> GetDashboardContentAsync(CancellationToken cancellationToken = default);
}
