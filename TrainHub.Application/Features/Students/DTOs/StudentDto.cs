namespace TrainHub.Application.Features.Students.DTOs;

/// <summary>
/// Response DTO representing a student.
/// </summary>
public class StudentDto
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public Guid OrganizationId { get; set; }

    public string StudentCode { get; set; } = string.Empty;

    public string? GuardianPhone { get; set; }

    public string? AcademicYear { get; set; }

    public DateOnly? DateOfBirth { get; set; }

    public int? Age { get; set; }

    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    // Associated User information for display
    public string? FirstName { get; set; }

    public string? LastName { get; set; }

    public string? Email { get; set; }

    public string? PhoneNumber { get; set; }
}
