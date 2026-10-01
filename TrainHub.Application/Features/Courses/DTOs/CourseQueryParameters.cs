using System.ComponentModel.DataAnnotations;
using TrainHub.Domain.Constants;

namespace TrainHub.Application.Features.Courses.DTOs;

/// <summary>
/// Query parameters for searching, filtering, sorting, and paginating Courses.
/// </summary>
public class CourseQueryParameters
{
    private int _pageNumber = CourseConstants.DefaultPageNumber;
    private int _pageSize = CourseConstants.DefaultPageSize;

    /// <summary>
    /// Current page number (1-based index). Defaults to 1.
    /// </summary>
    [Range(1, int.MaxValue, ErrorMessage = "Page number must be at least 1.")]
    public int PageNumber
    {
        get => _pageNumber;
        set => _pageNumber = value < 1 ? CourseConstants.DefaultPageNumber : value;
    }

    /// <summary>
    /// Number of items per page. Defaults to 10 (maximum 100).
    /// </summary>
    [Range(1, CourseConstants.MaxPageSize, ErrorMessage = "Page size must be between 1 and 100.")]
    public int PageSize
    {
        get => _pageSize;
        set => _pageSize = value switch
        {
            < 1 => CourseConstants.DefaultPageSize,
            > CourseConstants.MaxPageSize => CourseConstants.MaxPageSize,
            _ => value
        };
    }

    /// <summary>
    /// Free-text search term matched against course name, description, and category.
    /// </summary>
    [MaxLength(CourseConstants.SearchTermMaxLength, ErrorMessage = "Search term cannot exceed 100 characters.")]
    public string? SearchTerm { get; set; }

    /// <summary>
    /// Filter by course category.
    /// </summary>
    [MaxLength(CourseConstants.CategoryMaxLength, ErrorMessage = "Category filter cannot exceed 100 characters.")]
    public string? Category { get; set; }

    /// <summary>
    /// Filter by course status (0 = Draft, 1 = Active, 2 = Inactive, 3 = Archived).
    /// </summary>
    public byte? Status { get; set; }

    /// <summary>
    /// Filter courses with base price greater than or equal to this amount.
    /// </summary>
    [Range((double)CourseConstants.MinBasePrice, (double)CourseConstants.MaxBasePrice,
        ErrorMessage = "MinPrice must be between 0 and 1,000,000.")]
    public decimal? MinPrice { get; set; }

    /// <summary>
    /// Filter courses with base price less than or equal to this amount.
    /// </summary>
    [Range((double)CourseConstants.MinBasePrice, (double)CourseConstants.MaxBasePrice,
        ErrorMessage = "MaxPrice must be between 0 and 1,000,000.")]
    public decimal? MaxPrice { get; set; }

    /// <summary>
    /// Filter courses with duration in hours greater than or equal to this value.
    /// </summary>
    [Range(CourseConstants.MinDurationInHours, CourseConstants.MaxDurationInHours,
        ErrorMessage = "MinDuration must be between 1 and 10,000.")]
    public int? MinDuration { get; set; }

    /// <summary>
    /// Filter courses with duration in hours less than or equal to this value.
    /// </summary>
    [Range(CourseConstants.MinDurationInHours, CourseConstants.MaxDurationInHours,
        ErrorMessage = "MaxDuration must be between 1 and 10,000.")]
    public int? MaxDuration { get; set; }

    /// <summary>
    /// Field to sort by. Supported values: "CreatedAt", "UpdatedAt", "Name", "Category", "BasePrice", "DurationInHours", "Status".
    /// </summary>
    public string? SortBy { get; set; } = "CreatedAt";

    /// <summary>
    /// Whether to sort in descending order. Defaults to true.
    /// </summary>
    public bool SortDescending { get; set; } = true;
}
