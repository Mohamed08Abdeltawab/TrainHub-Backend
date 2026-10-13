using System.ComponentModel.DataAnnotations;
using TrainHub.Domain.Constants;

namespace TrainHub.Application.Features.Students.DTOs;

/// <summary>
/// Request DTO for updating an existing Student.
/// </summary>
public class UpdateStudentDto
{
    [Required(ErrorMessage = "StudentCode is required.")]
    [StringLength(StudentConstants.StudentCodeMaxLength, MinimumLength = StudentConstants.StudentCodeMinLength,
        ErrorMessage = "Student code must be between 3 and 50 characters.")]
    [RegularExpression(StudentConstants.StudentCodePattern,
        ErrorMessage = "Student code must contain only alphanumeric characters, underscores, or hyphens.")]
    public string StudentCode { get; set; } = string.Empty;

    [Phone(ErrorMessage = "Invalid phone number format.")]
    [MaxLength(StudentConstants.GuardianPhoneMaxLength, ErrorMessage = "Guardian phone number cannot exceed 20 characters.")]
    public string? GuardianPhone { get; set; }

    [MaxLength(StudentConstants.AcademicYearMaxLength, ErrorMessage = "Academic year cannot exceed 100 characters.")]
    public string? AcademicYear { get; set; }

    public DateOnly? DateOfBirth { get; set; }

    [MaxLength(StudentConstants.NotesMaxLength, ErrorMessage = "Notes cannot exceed 500 characters.")]
    public string? Notes { get; set; }

    [MaxLength(100)]
    public string? FirstName { get; set; }

    [MaxLength(100)]
    public string? LastName { get; set; }

    [Phone(ErrorMessage = "Invalid phone number format.")]
    [MaxLength(50)]
    public string? PhoneNumber { get; set; }
}
