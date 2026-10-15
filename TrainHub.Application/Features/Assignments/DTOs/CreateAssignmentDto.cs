using System.ComponentModel.DataAnnotations;
using TrainHub.Domain.Constants;

namespace TrainHub.Application.Features.Assignments.DTOs;

/// <summary>
/// Request DTO for creating a new assignment.
/// </summary>
public class CreateAssignmentDto
{
    [Required(ErrorMessage = "CourseGroupId is required.")]
    public Guid CourseGroupId { get; set; }

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
    public int MaxScore { get; set; } = AssignmentConstants.DefaultMaxScore;

    public byte Status { get; set; } = AssignmentConstants.DefaultStatus;
}
