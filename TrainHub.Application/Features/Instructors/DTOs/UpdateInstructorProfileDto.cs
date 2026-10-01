using System.ComponentModel.DataAnnotations;
using TrainHub.Domain.Constants;

namespace TrainHub.Application.Features.Instructors.DTOs;

/// <summary>
/// Request DTO for an instructor updating their personal profile.
/// Strictly limited to basic personal information (bio, phone).
/// Administrative fields (specialization, organization, active status, financial details) cannot be modified by the instructor.
/// </summary>
public class UpdateInstructorProfileDto
{
    [MaxLength(InstructorConstants.BioMaxLength, ErrorMessage = "Bio cannot exceed 2000 characters.")]
    public string? Bio { get; set; }

    [Phone(ErrorMessage = "Invalid phone number format.")]
    [MaxLength(20, ErrorMessage = "Phone number cannot exceed 20 characters.")]
    public string? PhoneNumber { get; set; }
}
