using System.ComponentModel.DataAnnotations;
using TrainHub.Domain.Constants;

namespace TrainHub.Application.Features.Students.DTOs;

/// <summary>
/// Query parameters for searching, filtering, sorting, and paginating Students.
/// </summary>
public class StudentQueryParameters
{
    private int _pageNumber = StudentConstants.DefaultPageNumber;
    private int _pageSize = StudentConstants.DefaultPageSize;

    /// <summary>
    /// Current page number (1-based index). Defaults to 1.
    /// </summary>
    [Range(1, int.MaxValue, ErrorMessage = "Page number must be at least 1.")]
    public int PageNumber
    {
        get => _pageNumber;
        set => _pageNumber = value < 1 ? StudentConstants.DefaultPageNumber : value;
    }

    /// <summary>
    /// Number of items per page. Defaults to 10 (maximum 100).
    /// </summary>
    [Range(1, StudentConstants.MaxPageSize, ErrorMessage = "Page size must be between 1 and 100.")]
    public int PageSize
    {
        get => _pageSize;
        set => _pageSize = value switch
        {
            < 1 => StudentConstants.DefaultPageSize,
            > StudentConstants.MaxPageSize => StudentConstants.MaxPageSize,
            _ => value
        };
    }

    /// <summary>
    /// Free-text search term matched against student code, first name, last name, email, and guardian phone.
    /// </summary>
    [MaxLength(StudentConstants.SearchTermMaxLength, ErrorMessage = "Search term cannot exceed 100 characters.")]
    public string? SearchTerm { get; set; }

    /// <summary>
    /// Filter by academic year (e.g. "Grade 10", "2025/2026").
    /// </summary>
    [MaxLength(StudentConstants.AcademicYearMaxLength, ErrorMessage = "Academic year filter cannot exceed 100 characters.")]
    public string? AcademicYear { get; set; }

    /// <summary>
    /// Filter students born on or after this date.
    /// </summary>
    public DateOnly? DateOfBirthFrom { get; set; }

    /// <summary>
    /// Filter students born on or before this date.
    /// </summary>
    public DateOnly? DateOfBirthTo { get; set; }

    /// <summary>
    /// Filter students with or without a registered guardian phone number.
    /// </summary>
    public bool? HasGuardianPhone { get; set; }

    /// <summary>
    /// Filter students by associated user account active state.
    /// </summary>
    public bool? IsActive { get; set; }

    /// <summary>
    /// Field to sort by. Supported values: "CreatedAt", "UpdatedAt", "StudentCode", "AcademicYear", "DateOfBirth", "FirstName", "LastName", "Email".
    /// </summary>
    public string? SortBy { get; set; } = "CreatedAt";

    /// <summary>
    /// Whether to sort in descending order. Defaults to true.
    /// </summary>
    public bool SortDescending { get; set; } = true;
}
