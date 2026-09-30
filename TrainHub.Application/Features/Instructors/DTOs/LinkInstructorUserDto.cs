using System.ComponentModel.DataAnnotations;

namespace TrainHub.Application.Features.Instructors.DTOs;

/// <summary>
/// Request DTO for linking an Instructor to a User account.
/// </summary>
public class LinkInstructorUserDto
{
    /// <summary>
    /// The ID of the ApplicationUser to link to this instructor.
    /// </summary>
    [Required(ErrorMessage = "UserId is required.")]
    public Guid UserId { get; set; }
}
