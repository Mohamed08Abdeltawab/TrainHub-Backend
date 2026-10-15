namespace TrainHub.Application.Features.Assignments.DTOs;

/// <summary>
/// Response DTO representing an assignment.
/// </summary>
public class AssignmentDto
{
    public Guid Id { get; set; }

    public Guid OrganizationId { get; set; }

    public Guid CourseGroupId { get; set; }

    public string CourseGroupName { get; set; } = string.Empty;

    public Guid CourseId { get; set; }

    public string CourseName { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    public DateTime DueDate { get; set; }

    public int MaxScore { get; set; }

    public byte Status { get; set; }

    public string StatusName { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}
