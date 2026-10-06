namespace TrainHub.Application.Features.Dashboard.DTOs;

/// <summary>
/// Dashboard statistics for courses and course groups (cohorts) within an organization.
/// </summary>
public class CourseStatisticsDto
{
    /// <summary>
    /// Total number of courses in the organization.
    /// </summary>
    public int TotalCourses { get; set; }

    /// <summary>
    /// Number of active courses.
    /// </summary>
    public int ActiveCourses { get; set; }

    /// <summary>
    /// Number of inactive courses.
    /// </summary>
    public int InactiveCourses { get; set; }

    /// <summary>
    /// Number of draft courses.
    /// </summary>
    public int DraftCourses { get; set; }

    /// <summary>
    /// Number of archived courses.
    /// </summary>
    public int ArchivedCourses { get; set; }

    /// <summary>
    /// Total number of course groups (cohorts).
    /// </summary>
    public int TotalCourseGroups { get; set; }

    /// <summary>
    /// Number of active course groups.
    /// </summary>
    public int ActiveCourseGroups { get; set; }

    /// <summary>
    /// Number of completed course groups.
    /// </summary>
    public int CompletedCourseGroups { get; set; }

    /// <summary>
    /// Number of cancelled course groups.
    /// </summary>
    public int CancelledCourseGroups { get; set; }

    /// <summary>
    /// Number of draft course groups.
    /// </summary>
    public int DraftCourseGroups { get; set; }

    /// <summary>
    /// Total number of enrollments across all course groups.
    /// </summary>
    public int TotalEnrollments { get; set; }

    /// <summary>
    /// Number of active enrollments across all course groups.
    /// </summary>
    public int ActiveEnrollments { get; set; }
}
