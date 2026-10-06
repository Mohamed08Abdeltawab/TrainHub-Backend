namespace TrainHub.Application.Features.Dashboard.DTOs;

/// <summary>
/// Dashboard statistics for attendance records and sessions within an organization.
/// </summary>
public class AttendanceStatisticsDto
{
    /// <summary>
    /// Optional CourseGroup ID if statistics are filtered by cohort.
    /// </summary>
    public Guid? CourseGroupId { get; set; }

    /// <summary>
    /// Total number of attendance records marked.
    /// </summary>
    public int TotalRecords { get; set; }

    /// <summary>
    /// Total attended count (Present + Late).
    /// </summary>
    public int AttendedCount { get; set; }

    /// <summary>
    /// Number of present attendance records.
    /// </summary>
    public int PresentCount { get; set; }

    /// <summary>
    /// Number of late attendance records.
    /// </summary>
    public int LateCount { get; set; }

    /// <summary>
    /// Number of absent attendance records.
    /// </summary>
    public int AbsentCount { get; set; }

    /// <summary>
    /// Number of excused attendance records.
    /// </summary>
    public int ExcusedCount { get; set; }

    /// <summary>
    /// Overall attendance percentage: (Present + Late) / TotalRecords * 100, rounded to 2 decimal places.
    /// Returns 0 when TotalRecords is 0.
    /// </summary>
    public decimal AttendancePercentage { get; set; }

    /// <summary>
    /// Total number of sessions scheduled or created.
    /// </summary>
    public int TotalSessions { get; set; }

    /// <summary>
    /// Number of scheduled sessions.
    /// </summary>
    public int ScheduledSessions { get; set; }

    /// <summary>
    /// Number of completed sessions.
    /// </summary>
    public int CompletedSessions { get; set; }

    /// <summary>
    /// Number of cancelled sessions.
    /// </summary>
    public int CancelledSessions { get; set; }

    /// <summary>
    /// Number of draft sessions.
    /// </summary>
    public int DraftSessions { get; set; }
}
