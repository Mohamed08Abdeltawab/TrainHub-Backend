using System.ComponentModel.DataAnnotations;
using TrainHub.Domain.Constants;

namespace TrainHub.Application.Features.Assignments.DTOs;

/// <summary>
/// Query parameters for searching, filtering, sorting, and paginating Assignments.
/// </summary>
public class AssignmentQueryParameters
{
    private int _pageNumber = 1;
    private int _pageSize = 10;

    [Range(1, int.MaxValue, ErrorMessage = "Page number must be at least 1.")]
    public int PageNumber
    {
        get => _pageNumber;
        set => _pageNumber = value < 1 ? 1 : value;
    }

    [Range(1, AssignmentConstants.MaxPageSize, ErrorMessage = "Page size must be between 1 and 100.")]
    public int PageSize
    {
        get => _pageSize;
        set => _pageSize = value switch
        {
            < 1 => 10,
            > AssignmentConstants.MaxPageSize => AssignmentConstants.MaxPageSize,
            _ => value
        };
    }

    public string? SearchTerm { get; set; }

    public Guid? CourseGroupId { get; set; }

    public Guid? CourseId { get; set; }

    public byte? Status { get; set; }

    public string? SortBy { get; set; } = "CreatedAt";

    public bool SortDescending { get; set; } = true;
}
