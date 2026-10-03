using System.ComponentModel.DataAnnotations;
using TrainHub.Domain.Constants;

namespace TrainHub.Application.Features.CourseGroups.DTOs;

/// <summary>
/// Request DTO for updating the student capacity of an existing CourseGroup.
/// </summary>
public class UpdateCourseGroupCapacityDto
{
    /// <summary>
    /// New capacity limit (between 1 and 1000).
    /// </summary>
    [Required(ErrorMessage = "Capacity is required.")]
    [Range(CourseGroupConstants.MinCapacity, CourseGroupConstants.MaxCapacity,
        ErrorMessage = "Capacity must be between 1 and 1000.")]
    public int Capacity { get; set; }
}
