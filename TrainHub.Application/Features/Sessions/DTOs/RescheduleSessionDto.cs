using System.ComponentModel.DataAnnotations;
using TrainHub.Domain.Constants;

namespace TrainHub.Application.Features.Sessions.DTOs;

/// <summary>
/// Request DTO for rescheduling an existing Session to new start/end times and optional room.
/// </summary>
public class RescheduleSessionDto
{
    [Required(ErrorMessage = "StartTime is required.")]
    public DateTime StartTime { get; set; }

    [Required(ErrorMessage = "EndTime is required.")]
    public DateTime EndTime { get; set; }

    [MaxLength(SessionConstants.RoomMaxLength, ErrorMessage = "Room cannot exceed 50 characters.")]
    public string? Room { get; set; }
}
