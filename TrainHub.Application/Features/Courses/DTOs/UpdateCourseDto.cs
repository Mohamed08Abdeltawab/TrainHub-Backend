using System.ComponentModel.DataAnnotations;
using TrainHub.Domain.Constants;

namespace TrainHub.Application.Features.Courses.DTOs;

/// <summary>
/// Request DTO for updating an existing Course.
/// </summary>
public class UpdateCourseDto
{
    [Required(ErrorMessage = "Course name is required.")]
    [StringLength(CourseConstants.NameMaxLength, MinimumLength = CourseConstants.NameMinLength,
        ErrorMessage = "Course name must be between 2 and 200 characters.")]
    public string Name { get; set; } = string.Empty;

    [MaxLength(CourseConstants.DescriptionMaxLength, ErrorMessage = "Description cannot exceed 4000 characters.")]
    public string? Description { get; set; }

    [StringLength(CourseConstants.CategoryMaxLength, MinimumLength = CourseConstants.CategoryMinLength,
        ErrorMessage = "Category must be between 2 and 100 characters.")]
    public string? Category { get; set; }

    [Range((double)CourseConstants.MinBasePrice, (double)CourseConstants.MaxBasePrice,
        ErrorMessage = "Base price must be between 0 and 1,000,000.")]
    public decimal BasePrice { get; set; }

    [Range(CourseConstants.MinDurationInHours, CourseConstants.MaxDurationInHours,
        ErrorMessage = "Duration in hours must be between 1 and 10,000.")]
    public int? DurationInHours { get; set; }

    [MaxLength(CourseConstants.ThumbnailUrlMaxLength, ErrorMessage = "Thumbnail URL cannot exceed 500 characters.")]
    [Url(ErrorMessage = "Thumbnail URL must be a valid URL.")]
    public string? ThumbnailUrl { get; set; }

    [Range(0, 3, ErrorMessage = "Status must be a valid course status (0 = Draft, 1 = Active, 2 = Inactive, 3 = Archived).")]
    public byte Status { get; set; } = CourseConstants.Statuses.Active;
}
