namespace TrainHub.Application.Features.Instructors.DTOs;

/// <summary>
/// Session model returned to instructors representing scheduled teaching sessions.
/// </summary>
public class InstructorSessionDto
{
    public Guid Id { get; set; }
    public Guid CourseGroupId { get; set; }
    public string CourseGroupName { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public string? Room { get; set; }
    public string Status { get; set; } = string.Empty;
}
