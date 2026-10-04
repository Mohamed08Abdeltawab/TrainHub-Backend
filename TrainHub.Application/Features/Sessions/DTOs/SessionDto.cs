namespace TrainHub.Application.Features.Sessions.DTOs;

/// <summary>
/// Response DTO representing a Session.
/// </summary>
public class SessionDto
{
    public Guid Id { get; set; }

    public Guid OrganizationId { get; set; }

    public Guid CourseGroupId { get; set; }

    public string CourseGroupName { get; set; } = string.Empty;

    public Guid CourseId { get; set; }

    public string CourseName { get; set; } = string.Empty;

    public Guid? InstructorId { get; set; }

    public string? InstructorName { get; set; }

    public DateTime StartTime { get; set; }

    public DateTime EndTime { get; set; }

    public string? Room { get; set; }

    public byte Status { get; set; }

    public string StatusName { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}
