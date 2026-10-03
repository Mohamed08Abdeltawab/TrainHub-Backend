using System.ComponentModel.DataAnnotations;
using TrainHub.Domain.Constants;

namespace TrainHub.Application.Features.CourseGroups.DTOs;

/// <summary>
/// Query parameters for searching, filtering, sorting, and paginating CourseGroups.
/// </summary>
public class CourseGroupQueryParameters
{
    private int _pageNumber = CourseGroupConstants.DefaultPageNumber;
    private int _pageSize = CourseGroupConstants.DefaultPageSize;

    /// <summary>
    /// Current page number (1-based index). Defaults to 1.
    /// </summary>
    [Range(1, int.MaxValue, ErrorMessage = "Page number must be at least 1.")]
    public int PageNumber
    {
        get => _pageNumber;
        set => _pageNumber = value < 1 ? CourseGroupConstants.DefaultPageNumber : value;
    }

    /// <summary>
    /// Number of items per page. Defaults to 10 (maximum 100).
    /// </summary>
    [Range(1, CourseGroupConstants.MaxPageSize, ErrorMessage = "Page size must be between 1 and 100.")]
    public int PageSize
    {
        get => _pageSize;
        set => _pageSize = value switch
        {
            < 1 => CourseGroupConstants.DefaultPageSize,
            > CourseGroupConstants.MaxPageSize => CourseGroupConstants.MaxPageSize,
            _ => value
        };
    }

    /// <summary>
    /// Free-text search term matched against course group name and schedule description.
    /// </summary>
    [MaxLength(CourseGroupConstants.SearchTermMaxLength, ErrorMessage = "Search term cannot exceed 100 characters.")]
    public string? SearchTerm { get; set; }

    /// <summary>
    /// Filter by course ID.
    /// </summary>
    public Guid? CourseId { get; set; }

    /// <summary>
    /// Filter by instructor ID.
    /// </summary>
    public Guid? InstructorId { get; set; }

    /// <summary>
    /// Field to sort by. Supported values: "CreatedAt", "UpdatedAt", "Name", "StartDate", "EndDate".
    /// </summary>
    public string? SortBy { get; set; } = "CreatedAt";

    /// <summary>
    /// Whether to sort in descending order. Defaults to true.
    /// </summary>
    public bool SortDescending { get; set; } = true;
}
