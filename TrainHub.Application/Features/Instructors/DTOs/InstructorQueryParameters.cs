using System.ComponentModel.DataAnnotations;
using TrainHub.Domain.Constants;

namespace TrainHub.Application.Features.Instructors.DTOs;

/// <summary>
/// Query parameters for searching, filtering, sorting, and paginating Instructors.
/// </summary>
public class InstructorQueryParameters
{
    private int _pageNumber = InstructorConstants.DefaultPageNumber;
    private int _pageSize = InstructorConstants.DefaultPageSize;

    /// <summary>
    /// Current page number (1-based index). Defaults to 1.
    /// </summary>
    [Range(1, int.MaxValue, ErrorMessage = "Page number must be at least 1.")]
    public int PageNumber
    {
        get => _pageNumber;
        set => _pageNumber = value < 1 ? InstructorConstants.DefaultPageNumber : value;
    }

    /// <summary>
    /// Number of items per page. Defaults to 10 (maximum 100).
    /// </summary>
    [Range(1, InstructorConstants.MaxPageSize, ErrorMessage = "Page size must be between 1 and 100.")]
    public int PageSize
    {
        get => _pageSize;
        set => _pageSize = value switch
        {
            < 1 => InstructorConstants.DefaultPageSize,
            > InstructorConstants.MaxPageSize => InstructorConstants.MaxPageSize,
            _ => value
        };
    }

    /// <summary>
    /// Free-text search term matched against specialization, bio, first name, last name, email, and phone number.
    /// </summary>
    [MaxLength(InstructorConstants.SearchTermMaxLength, ErrorMessage = "Search term cannot exceed 100 characters.")]
    public string? SearchTerm { get; set; }

    /// <summary>
    /// Filter by specialization (e.g. "Full Stack .NET", "Mathematics").
    /// </summary>
    [MaxLength(InstructorConstants.SpecializationMaxLength, ErrorMessage = "Specialization filter cannot exceed 150 characters.")]
    public string? Specialization { get; set; }

    /// <summary>
    /// Filter instructors by associated user account active state.
    /// </summary>
    public bool? IsActive { get; set; }

    /// <summary>
    /// Filter instructors by whether they have a bio filled out.
    /// </summary>
    public bool? HasBio { get; set; }

    /// <summary>
    /// Field to sort by. Supported values: "CreatedAt", "UpdatedAt", "Specialization", "FirstName", "LastName", "Email".
    /// </summary>
    public string? SortBy { get; set; } = "CreatedAt";

    /// <summary>
    /// Whether to sort in descending order. Defaults to true.
    /// </summary>
    public bool SortDescending { get; set; } = true;
}
