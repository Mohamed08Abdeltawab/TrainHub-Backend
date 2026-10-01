namespace TrainHub.Application.Features.Courses.DTOs;

/// <summary>
/// Response DTO representing a course.
/// </summary>
public class CourseDto
{
    public Guid Id { get; set; }

    public Guid OrganizationId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string? Category { get; set; }

    public decimal BasePrice { get; set; }

    public int? DurationInHours { get; set; }

    public string? ThumbnailUrl { get; set; }

    public byte Status { get; set; }

    public string StatusName { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public int CourseGroupsCount { get; set; }
}
