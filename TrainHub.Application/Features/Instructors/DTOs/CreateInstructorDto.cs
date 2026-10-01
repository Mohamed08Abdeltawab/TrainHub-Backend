using System.ComponentModel.DataAnnotations;
using TrainHub.Domain.Constants;

namespace TrainHub.Application.Features.Instructors.DTOs;

/// <summary>
/// Request DTO for creating a new Instructor.
/// </summary>
public class CreateInstructorDto
{
    [Required(ErrorMessage = "UserId is required.")]
    public Guid UserId { get; set; }

    [Required(ErrorMessage = "Specialization is required.")]
    [StringLength(InstructorConstants.SpecializationMaxLength, MinimumLength = InstructorConstants.SpecializationMinLength,
        ErrorMessage = "Specialization must be between 2 and 150 characters.")]
    public string Specialization { get; set; } = string.Empty;

    [MaxLength(InstructorConstants.BioMaxLength, ErrorMessage = "Bio cannot exceed 2000 characters.")]
    public string? Bio { get; set; }
}
