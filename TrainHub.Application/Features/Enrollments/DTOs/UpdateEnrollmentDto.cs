using System.ComponentModel.DataAnnotations;
using TrainHub.Domain.Constants;

namespace TrainHub.Application.Features.Enrollments.DTOs;

/// <summary>
/// Request DTO for updating an existing Enrollment.
/// </summary>
public class UpdateEnrollmentDto
{
    [Range((double)EnrollmentConstants.MinPaidAmount, (double)EnrollmentConstants.MaxPaidAmount,
        ErrorMessage = "PaidAmount must be between 0 and 1,000,000.")]
    public decimal? PaidAmount { get; set; }

    [Range(0, 3, ErrorMessage = "Status must be a valid enrollment status (0 = Pending, 1 = Active, 2 = Completed, 3 = Cancelled).")]
    public byte? Status { get; set; }
}
