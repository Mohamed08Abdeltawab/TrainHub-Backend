namespace TrainHub.Application.Features.Enrollments.DTOs;

/// <summary>
/// Response DTO representing an Enrollment.
/// </summary>
public class EnrollmentDto
{
    public Guid Id { get; set; }

    public Guid OrganizationId { get; set; }

    public Guid CourseGroupId { get; set; }

    public string CourseGroupName { get; set; } = string.Empty;

    public Guid CourseId { get; set; }

    public string CourseName { get; set; } = string.Empty;

    public Guid StudentId { get; set; }

    public string StudentCode { get; set; } = string.Empty;

    public string StudentName { get; set; } = string.Empty;

    public string? StudentEmail { get; set; }

    public DateTime EnrollmentDate { get; set; }

    public decimal PaidAmount { get; set; }

    public byte Status { get; set; }

    public string StatusName { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}
