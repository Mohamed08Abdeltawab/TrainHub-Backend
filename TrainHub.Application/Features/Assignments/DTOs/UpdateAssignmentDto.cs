using System.ComponentModel.DataAnnotations;
using TrainHub.Domain.Constants;

namespace TrainHub.Application.Features.Assignments.DTOs;

/// <summary>
/// Request DTO for updating an existing assignment.
/// </summary>
public class UpdateAssignmentDto
{
    [Required(ErrorMessage = "Title is required.")]
    [StringLength(AssignmentConstants.TitleMaxLength, MinimumLength = AssignmentConstants.TitleMinLength,
        ErrorMessage = "Title must be between 2 and 200 characters.")]
    public string Title { get; set; } = string.Empty;

    [MaxLength(AssignmentConstants.DescriptionMaxLength,
        ErrorMessage = "Description cannot exceed 1000 characters.")]
    public string? Description { get; set; }

    [Required(ErrorMessage = "DueDate is required.")]
    public DateTime DueDate { get; set; }

    [Range(AssignmentConstants.MinScore, AssignmentConstants.MaxScore,
        ErrorMessage = "MaxScore must be between 1 and 1000.")]
    public int? MaxScore { get; set; }

    public byte? Status { get; set; }
}
