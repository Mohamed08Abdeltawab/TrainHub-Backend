namespace TrainHub.Application.Features.CourseGroups.DTOs;

/// <summary>
/// Request DTO for assigning or unassigning an instructor to a CourseGroup.
/// </summary>
public class AssignInstructorDto
{
    /// <summary>
    /// The unique ID of the instructor to assign, or null to unassign.
    /// </summary>
    public Guid? InstructorId { get; set; }
}
