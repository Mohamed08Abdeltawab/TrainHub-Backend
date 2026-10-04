using System.ComponentModel.DataAnnotations;
using TrainHub.Domain.Constants;

namespace TrainHub.Application.Features.Sessions.DTOs;

/// <summary>
/// Query parameters for searching, filtering, sorting, and paginating Sessions.
/// </summary>
public class SessionQueryParameters
{
    private int _pageNumber = SessionConstants.DefaultPageNumber;
    private int _pageSize = SessionConstants.DefaultPageSize;

    /// <summary>
    /// Current page number (1-based index). Defaults to 1.
    /// </summary>
    [Range(1, int.MaxValue, ErrorMessage = "Page number must be at least 1.")]
    public int PageNumber
    {
        get => _pageNumber;
        set => _pageNumber = value < 1 ? SessionConstants.DefaultPageNumber : value;
    }

    /// <summary>
    /// Number of items per page. Defaults to 10 (maximum 100).
    /// </summary>
    [Range(1, SessionConstants.MaxPageSize, ErrorMessage = "Page size must be between 1 and 100.")]
    public int PageSize
    {
        get => _pageSize;
        set => _pageSize = value switch
        {
            < 1 => SessionConstants.DefaultPageSize,
            > SessionConstants.MaxPageSize => SessionConstants.MaxPageSize,
            _ => value
        };
    }

    /// <summary>
    /// Optional search term matched against Room or Course Group name.
    /// </summary>
    [MaxLength(SessionConstants.SearchTermMaxLength, ErrorMessage = "Search term cannot exceed 100 characters.")]
    public string? SearchTerm { get; set; }

    /// <summary>
    /// Filter by CourseGroup ID.
    /// </summary>
    public Guid? CourseGroupId { get; set; }

    /// <summary>
    /// Filter by parent Course ID.
    /// </summary>
    public Guid? CourseId { get; set; }

    /// <summary>
    /// Filter by Instructor ID.
    /// </summary>
    public Guid? InstructorId { get; set; }

    /// <summary>
    /// Filter by status (0 = Draft, 1 = Scheduled, 2 = Completed, 3 = Cancelled).
    /// </summary>
    public byte? Status { get; set; }

    /// <summary>
    /// Filter sessions starting on or after this date/time (UTC).
    /// </summary>
    public DateTime? FromDate { get; set; }

    /// <summary>
    /// Filter sessions starting on or before this date/time (UTC).
    /// </summary>
    public DateTime? ToDate { get; set; }

    /// <summary>
    /// Filter by room name.
    /// </summary>
    public string? Room { get; set; }

    /// <summary>
    /// Field to sort by. Supported values: "CreatedAt", "UpdatedAt", "StartTime", "EndTime", "Room", "Status".
    /// </summary>
    public string? SortBy { get; set; } = "StartTime";

    /// <summary>
    /// Whether to sort in descending order. Defaults to false (chronological order for sessions).
    /// </summary>
    public bool SortDescending { get; set; } = false;
}
