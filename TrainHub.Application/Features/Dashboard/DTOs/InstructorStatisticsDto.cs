namespace TrainHub.Application.Features.Dashboard.DTOs;

/// <summary>
/// Dashboard statistics for instructors within an organization.
/// </summary>
public class InstructorStatisticsDto
{
    /// <summary>
    /// Total number of instructors registered in the organization.
    /// </summary>
    public int TotalInstructors { get; set; }

    /// <summary>
    /// Number of active instructors (associated user account is active).
    /// </summary>
    public int ActiveInstructors { get; set; }

    /// <summary>
    /// Number of inactive instructors (associated user account is inactive).
    /// </summary>
    public int InactiveInstructors { get; set; }

    /// <summary>
    /// Number of instructors assigned to at least one course group.
    /// </summary>
    public int AssignedInstructors { get; set; }

    /// <summary>
    /// Number of instructors assigned to at least one active course group.
    /// </summary>
    public int ActiveAssignedInstructors { get; set; }
}
