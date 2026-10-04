using System.ComponentModel.DataAnnotations;
using TrainHub.Domain.Constants;

namespace TrainHub.Application.Features.Sessions.DTOs;

/// <summary>
/// Request DTO for creating a new Session within a CourseGroup.
/// </summary>
public class CreateSessionDto
{
    [Required(ErrorMessage = "CourseGroupId is required.")]
    public Guid CourseGroupId { get; set; }

    public Guid? InstructorId { get; set; }

    [Required(ErrorMessage = "StartTime is required.")]
    public DateTime StartTime { get; set; }

    [Required(ErrorMessage = "EndTime is required.")]
    public DateTime EndTime { get; set; }

    [MaxLength(SessionConstants.RoomMaxLength, ErrorMessage = "Room cannot exceed 50 characters.")]
    public string? Room { get; set; }

    [Range(0, 3, ErrorMessage = "Status must be a valid session status (0 = Draft, 1 = Scheduled, 2 = Completed, 3 = Cancelled).")]
    public byte Status { get; set; } = SessionConstants.DefaultStatus;
}
