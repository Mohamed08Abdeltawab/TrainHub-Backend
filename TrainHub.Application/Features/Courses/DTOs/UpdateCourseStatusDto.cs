using System.ComponentModel.DataAnnotations;
using TrainHub.Domain.Constants;

namespace TrainHub.Application.Features.Courses.DTOs;

/// <summary>
/// Request DTO for updating the status of an existing Course.
/// </summary>
public class UpdateCourseStatusDto
{
    /// <summary>
    /// Target lifecycle status (0 = Draft, 1 = Active, 2 = Inactive, 3 = Archived).
    /// </summary>
    [Required(ErrorMessage = "Status is required.")]
    [Range(0, 3, ErrorMessage = "Status must be a valid course status (0 = Draft, 1 = Active, 2 = Inactive, 3 = Archived).")]
    public byte Status { get; set; }
}
