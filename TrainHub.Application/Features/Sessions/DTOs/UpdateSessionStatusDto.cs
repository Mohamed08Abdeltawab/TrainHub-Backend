using System.ComponentModel.DataAnnotations;

namespace TrainHub.Application.Features.Sessions.DTOs;

/// <summary>
/// Request DTO for updating the status of an existing Session.
/// </summary>
public class UpdateSessionStatusDto
{
    /// <summary>
    /// Target lifecycle status (0 = Draft, 1 = Scheduled, 2 = Completed, 3 = Cancelled).
    /// </summary>
    [Required(ErrorMessage = "Status is required.")]
    [Range(0, 3, ErrorMessage = "Status must be a valid session status (0 = Draft, 1 = Scheduled, 2 = Completed, 3 = Cancelled).")]
    public byte Status { get; set; }
}
