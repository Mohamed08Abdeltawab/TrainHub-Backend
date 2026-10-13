namespace TrainHub.Application.Features.Instructors.DTOs;

/// <summary>
/// Course model returned to instructors representing curricula assigned to them.
/// </summary>
public class InstructorCourseDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Level { get; set; } = "Intermediate";
    public int DurationHours { get; set; }
    public string Status { get; set; } = "Active";
    public DateTime CreatedAt { get; set; }
    public int ActiveCohortsCount { get; set; }
    public int TotalStudentsCount { get; set; }
}
