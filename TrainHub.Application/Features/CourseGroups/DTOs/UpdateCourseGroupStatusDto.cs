using System.ComponentModel.DataAnnotations;

namespace TrainHub.Application.Features.CourseGroups.DTOs;

/// <summary>
/// Request DTO for updating the status of an existing CourseGroup.
/// </summary>
public class UpdateCourseGroupStatusDto
{
    /// <summary>
    /// Target lifecycle status (0 = Draft, 1 = Active, 2 = Completed, 3 = Cancelled).
    /// </summary>
    [Required(ErrorMessage = "Status is required.")]
    [Range(0, 3, ErrorMessage = "Status must be a valid course group status (0 = Draft, 1 = Active, 2 = Completed, 3 = Cancelled).")]
    public byte Status { get; set; }
}
