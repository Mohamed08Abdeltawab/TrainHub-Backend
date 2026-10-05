using System.ComponentModel.DataAnnotations;
using TrainHub.Domain.Constants;

namespace TrainHub.Application.Features.Attendances.DTOs;

/// <summary>
/// Query parameters for searching, filtering, sorting, and paginating Attendance records.
/// </summary>
public class AttendanceQueryParameters
{
    private int _pageNumber = AttendanceConstants.DefaultPageNumber;
    private int _pageSize = AttendanceConstants.DefaultPageSize;

    /// <summary>
    /// Current page number (1-based index). Defaults to 1.
    /// </summary>
    [Range(1, int.MaxValue, ErrorMessage = "Page number must be at least 1.")]
    public int PageNumber
    {
        get => _pageNumber;
        set => _pageNumber = value < 1 ? AttendanceConstants.DefaultPageNumber : value;
    }

    /// <summary>
    /// Number of items per page. Defaults to 10 (maximum 100).
    /// </summary>
    [Range(1, AttendanceConstants.MaxPageSize, ErrorMessage = "Page size must be between 1 and 100.")]
    public int PageSize
    {
        get => _pageSize;
        set => _pageSize = value switch
        {
            < 1 => AttendanceConstants.DefaultPageSize,
            > AttendanceConstants.MaxPageSize => AttendanceConstants.MaxPageSize,
            _ => value
        };
    }

    /// <summary>
    /// Filter by Session ID.
    /// </summary>
    public Guid? SessionId { get; set; }

    /// <summary>
    /// Filter by Student ID.
    /// </summary>
    public Guid? StudentId { get; set; }

    /// <summary>
    /// Filter by CourseGroup ID (via Session).
    /// </summary>
    public Guid? CourseGroupId { get; set; }

    /// <summary>
    /// Filter by status (0 = Absent, 1 = Present, 2 = Late, 3 = Excused).
    /// </summary>
    public byte? Status { get; set; }

    /// <summary>
    /// Filter attendance marked on or after this date/time (UTC).
    /// </summary>
    public DateTime? FromDate { get; set; }

    /// <summary>
    /// Filter attendance marked on or before this date/time (UTC).
    /// </summary>
    public DateTime? ToDate { get; set; }

    /// <summary>
    /// Field to sort by. Supported values: "MarkedAt", "Status", "StudentId", "SessionId".
    /// </summary>
    public string? SortBy { get; set; } = "MarkedAt";

    /// <summary>
    /// Whether to sort in descending order. Defaults to true (most recent first).
    /// </summary>
    public bool SortDescending { get; set; } = true;
}
