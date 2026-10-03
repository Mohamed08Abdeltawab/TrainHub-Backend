using System.ComponentModel.DataAnnotations;
using TrainHub.Domain.Constants;

namespace TrainHub.Application.Features.Enrollments.DTOs;

/// <summary>
/// Query parameters for filtering, sorting, and paginating Enrollments.
/// </summary>
public class EnrollmentQueryParameters
{
    private int _pageNumber = EnrollmentConstants.DefaultPageNumber;
    private int _pageSize = EnrollmentConstants.DefaultPageSize;

    /// <summary>
    /// Current page number (1-based index). Defaults to 1.
    /// </summary>
    [Range(1, int.MaxValue, ErrorMessage = "Page number must be at least 1.")]
    public int PageNumber
    {
        get => _pageNumber;
        set => _pageNumber = value < 1 ? EnrollmentConstants.DefaultPageNumber : value;
    }

    /// <summary>
    /// Number of items per page. Defaults to 10 (maximum 100).
    /// </summary>
    [Range(1, EnrollmentConstants.MaxPageSize, ErrorMessage = "Page size must be between 1 and 100.")]
    public int PageSize
    {
        get => _pageSize;
        set => _pageSize = value switch
        {
            < 1 => EnrollmentConstants.DefaultPageSize,
            > EnrollmentConstants.MaxPageSize => EnrollmentConstants.MaxPageSize,
            _ => value
        };
    }

    /// <summary>
    /// Filter by CourseGroup ID.
    /// </summary>
    public Guid? CourseGroupId { get; set; }

    /// <summary>
    /// Filter by Student ID.
    /// </summary>
    public Guid? StudentId { get; set; }

    /// <summary>
    /// Filter by parent Course ID.
    /// </summary>
    public Guid? CourseId { get; set; }

    /// <summary>
    /// Filter by enrollment status (0 = Pending, 1 = Active, 2 = Completed, 3 = Cancelled).
    /// </summary>
    public byte? Status { get; set; }

    /// <summary>
    /// Field to sort by. Supported values: "CreatedAt", "UpdatedAt", "EnrollmentDate", "PaidAmount", "Status".
    /// </summary>
    public string? SortBy { get; set; } = "CreatedAt";

    /// <summary>
    /// Whether to sort in descending order. Defaults to true.
    /// </summary>
    public bool SortDescending { get; set; } = true;
}
