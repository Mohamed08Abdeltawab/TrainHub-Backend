namespace TrainHub.Application.Features.Dashboard.DTOs;

/// <summary>
/// Comprehensive dashboard summary containing all key statistics for an organization.
/// </summary>
public class DashboardSummaryDto
{
    /// <summary>
    /// ID of the organization the statistics belong to.
    /// </summary>
    public Guid OrganizationId { get; set; }

    /// <summary>
    /// Student statistics summary.
    /// </summary>
    public StudentStatisticsDto Students { get; set; } = new();

    /// <summary>
    /// Course and cohort statistics summary.
    /// </summary>
    public CourseStatisticsDto Courses { get; set; } = new();

    /// <summary>
    /// Instructor statistics summary.
    /// </summary>
    public InstructorStatisticsDto Instructors { get; set; } = new();

    /// <summary>
    /// Attendance and session statistics summary.
    /// </summary>
    public AttendanceStatisticsDto Attendance { get; set; } = new();
}
