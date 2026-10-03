using System.ComponentModel.DataAnnotations;
using TrainHub.Domain.Constants;

namespace TrainHub.Application.Features.CourseGroups.DTOs;

/// <summary>
/// Request DTO for creating a new CourseGroup.
/// </summary>
public class CreateCourseGroupDto
{
    [Required(ErrorMessage = "CourseId is required.")]
    public Guid CourseId { get; set; }

    public Guid? InstructorId { get; set; }

    [Required(ErrorMessage = "Course group name is required.")]
    [StringLength(CourseGroupConstants.NameMaxLength, MinimumLength = CourseGroupConstants.NameMinLength,
        ErrorMessage = "Course group name must be between 2 and 100 characters.")]
    public string Name { get; set; } = string.Empty;

    [MaxLength(CourseGroupConstants.ScheduleDescriptionMaxLength,
        ErrorMessage = "Schedule description cannot exceed 250 characters.")]
    public string? ScheduleDescription { get; set; }

    [Required(ErrorMessage = "StartDate is required.")]
    public DateOnly StartDate { get; set; }

    [Required(ErrorMessage = "EndDate is required.")]
    public DateOnly EndDate { get; set; }

    [Range(CourseGroupConstants.MinCapacity, CourseGroupConstants.MaxCapacity,
        ErrorMessage = "Capacity must be between 1 and 1000.")]
    public int? Capacity { get; set; }
}
