namespace TrainHub.Application.Features.Dashboard.DTOs;

/// <summary>
/// Dashboard statistics for students and student enrollments within an organization.
/// </summary>
public class StudentStatisticsDto
{
    /// <summary>
    /// Total number of students registered in the organization.
    /// </summary>
    public int TotalStudents { get; set; }

    /// <summary>
    /// Number of active students (associated user account is active).
    /// </summary>
    public int ActiveStudents { get; set; }

    /// <summary>
    /// Number of inactive students (associated user account is inactive).
    /// </summary>
    public int InactiveStudents { get; set; }

    /// <summary>
    /// Total number of student enrollments within the organization.
    /// </summary>
    public int TotalEnrollments { get; set; }

    /// <summary>
    /// Number of active enrollments.
    /// </summary>
    public int ActiveEnrollments { get; set; }

    /// <summary>
    /// Number of completed enrollments.
    /// </summary>
    public int CompletedEnrollments { get; set; }

    /// <summary>
    /// Number of cancelled enrollments.
    /// </summary>
    public int CancelledEnrollments { get; set; }

    /// <summary>
    /// Number of pending enrollments.
    /// </summary>
    public int PendingEnrollments { get; set; }

    /// <summary>
    /// Number of distinct students with at least one active enrollment.
    /// </summary>
    public int StudentsWithActiveEnrollments { get; set; }
}
