using System.ComponentModel.DataAnnotations;
using TrainHub.Domain.Constants;

namespace TrainHub.Application.Features.Enrollments.DTOs;

/// <summary>
/// Request DTO for enrolling a Student into a CourseGroup.
/// </summary>
public class CreateEnrollmentDto
{
    [Required(ErrorMessage = "CourseGroupId is required.")]
    public Guid CourseGroupId { get; set; }

    [Required(ErrorMessage = "StudentId is required.")]
    public Guid StudentId { get; set; }

    [Range((double)EnrollmentConstants.MinPaidAmount, (double)EnrollmentConstants.MaxPaidAmount,
        ErrorMessage = "PaidAmount must be between 0 and 1,000,000.")]
    public decimal? PaidAmount { get; set; }

    [Range(0, 3, ErrorMessage = "Status must be a valid enrollment status (0 = Pending, 1 = Active, 2 = Completed, 3 = Cancelled).")]
    public byte Status { get; set; } = EnrollmentConstants.DefaultStatus;

    public DateTime? EnrollmentDate { get; set; }
}
