namespace TrainHub.Application.Features.CourseGroups.DTOs;

/// <summary>
/// Response DTO representing a course group.
/// </summary>
public class CourseGroupDto
{
    public Guid Id { get; set; }

    public Guid OrganizationId { get; set; }

    public Guid CourseId { get; set; }

    public string CourseName { get; set; } = string.Empty;

    public Guid? InstructorId { get; set; }

    public string? InstructorName { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? ScheduleDescription { get; set; }

    public DateOnly StartDate { get; set; }

    public DateOnly EndDate { get; set; }

    public int Capacity { get; set; }

    public int EnrolledStudentsCount { get; set; }

    public int AvailableSeats { get; set; }

    public bool IsFull { get; set; }

    public byte Status { get; set; }

    public string StatusName { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}
