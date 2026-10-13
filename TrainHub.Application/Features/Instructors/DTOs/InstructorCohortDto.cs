namespace TrainHub.Application.Features.Instructors.DTOs;

/// <summary>
/// Cohort model returned to instructors representing groups assigned to them.
/// </summary>
public class InstructorCohortDto
{
    public Guid Id { get; set; }
    public Guid CourseId { get; set; }
    public string CourseName { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public string? ScheduleDescription { get; set; }
    public int Capacity { get; set; }
    public int EnrolledStudentsCount { get; set; }
    public string Status { get; set; } = string.Empty;
}
