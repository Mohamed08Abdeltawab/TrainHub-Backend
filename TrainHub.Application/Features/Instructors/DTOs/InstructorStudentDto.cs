namespace TrainHub.Application.Features.Instructors.DTOs;

/// <summary>
/// Student model returned to instructors representing learners in their assigned cohorts.
/// </summary>
public class InstructorStudentDto
{
    public Guid Id { get; set; }
    public string StudentCode { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string CourseGroupName { get; set; } = string.Empty;
    public string EnrollmentStatus { get; set; } = string.Empty;
}
