namespace TrainHub.Application.Features.Attendances.DTOs;

/// <summary>
/// Response DTO representing a student's attendance percentage and attendance breakdown.
/// </summary>
public class AttendancePercentageDto
{
    /// <summary>
    /// Unique ID of the student.
    /// </summary>
    public Guid StudentId { get; set; }

    /// <summary>
    /// Full name of the student.
    /// </summary>
    public string StudentName { get; set; } = string.Empty;

    /// <summary>
    /// Student code within the organization.
    /// </summary>
    public string StudentCode { get; set; } = string.Empty;

    /// <summary>
    /// Optional CourseGroup ID if percentage is filtered by cohort.
    /// </summary>
    public Guid? CourseGroupId { get; set; }

    /// <summary>
    /// Optional CourseGroup name if percentage is filtered by cohort.
    /// </summary>
    public string? CourseGroupName { get; set; }

    /// <summary>
    /// Total number of attendance records recorded for this student (denominator).
    /// </summary>
    public int TotalRecords { get; set; }

    /// <summary>
    /// Total sessions attended (Present + Late).
    /// </summary>
    public int AttendedCount { get; set; }

    /// <summary>
    /// Number of sessions where the student was Present.
    /// </summary>
    public int PresentCount { get; set; }

    /// <summary>
    /// Number of sessions where the student was Late.
    /// </summary>
    public int LateCount { get; set; }

    /// <summary>
    /// Number of sessions where the student was Absent.
    /// </summary>
    public int AbsentCount { get; set; }

    /// <summary>
    /// Number of sessions where the student was Excused.
    /// </summary>
    public int ExcusedCount { get; set; }

    /// <summary>
    /// Calculated attendance percentage: (Present + Late) / TotalRecords * 100, rounded to 2 decimal places.
    /// Returns 0 when TotalRecords is 0.
    /// </summary>
    public decimal AttendancePercentage { get; set; }
}
