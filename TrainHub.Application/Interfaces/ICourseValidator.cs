using TrainHub.Application.Features.Courses.DTOs;

namespace TrainHub.Application.Interfaces;

/// <summary>
/// Contract for validating Course-related business rules and data integrity.
/// </summary>
public interface ICourseValidator
{
    /// <summary>
    /// Validates business rules before creating a course (e.g., uniqueness of name within organization, field bounds).
    /// </summary>
    Task ValidateCreateAsync(CreateCourseDto dto, Guid organizationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates business rules before updating a course (e.g., existence, uniqueness of name within organization, field bounds).
    /// </summary>
    Task ValidateUpdateAsync(Guid id, UpdateCourseDto dto, Guid organizationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates business rules before deleting a course (e.g., existence, ensuring no dependent course groups exist).
    /// </summary>
    Task ValidateDeleteAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates query, filtering, search, sorting, and pagination parameters.
    /// </summary>
    void ValidateQuery(CourseQueryParameters query);
}
