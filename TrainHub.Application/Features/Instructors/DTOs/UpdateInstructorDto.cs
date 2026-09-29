using System.ComponentModel.DataAnnotations;
using TrainHub.Domain.Constants;

namespace TrainHub.Application.Features.Instructors.DTOs;

/// <summary>
/// Request DTO for updating an existing Instructor.
/// </summary>
public class UpdateInstructorDto
{
    [Required(ErrorMessage = "Specialization is required.")]
    [StringLength(InstructorConstants.SpecializationMaxLength, MinimumLength = InstructorConstants.SpecializationMinLength,
        ErrorMessage = "Specialization must be between 2 and 150 characters.")]
    public string Specialization { get; set; } = string.Empty;

    [MaxLength(InstructorConstants.BioMaxLength, ErrorMessage = "Bio cannot exceed 2000 characters.")]
    public string? Bio { get; set; }
}
