namespace TrainHub.Application.Features.Instructors.DTOs;

/// <summary>
/// Response DTO representing an instructor.
/// </summary>
public class InstructorDto
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public Guid OrganizationId { get; set; }

    public string Specialization { get; set; } = string.Empty;

    public string? Bio { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    // Associated User information for display
    public string? FirstName { get; set; }

    public string? LastName { get; set; }

    public string? Email { get; set; }

    public string? PhoneNumber { get; set; }
}
