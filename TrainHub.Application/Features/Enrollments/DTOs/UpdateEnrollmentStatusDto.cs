using System.ComponentModel.DataAnnotations;

namespace TrainHub.Application.Features.Enrollments.DTOs;

/// <summary>
/// Request DTO for updating the status of an existing Enrollment.
/// </summary>
public class UpdateEnrollmentStatusDto
{
    /// <summary>
    /// Target lifecycle status (0 = Pending, 1 = Active, 2 = Completed, 3 = Cancelled).
    /// </summary>
    [Required(ErrorMessage = "Status is required.")]
    [Range(0, 3, ErrorMessage = "Status must be a valid enrollment status (0 = Pending, 1 = Active, 2 = Completed, 3 = Cancelled).")]
    public byte Status { get; set; }
}
