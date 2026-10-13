namespace TrainHub.Application.Features.Students.DTOs;

/// <summary>
/// Course model returned to enrolled students, including cohort details and progress.
/// </summary>
public class StudentCourseDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Level { get; set; } = "Beginner";
    public int DurationHours { get; set; }
    public string Status { get; set; } = "Active";
    public DateTime CreatedAt { get; set; }
    public string? InstructorName { get; set; }
    public int ProgressPercent { get; set; }
    public string EnrollmentStatus { get; set; } = "Active";
    public Guid CourseGroupId { get; set; }
    public string CourseGroupName { get; set; } = string.Empty;
}
