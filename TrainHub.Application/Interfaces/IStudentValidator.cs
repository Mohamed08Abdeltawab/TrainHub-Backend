using TrainHub.Application.Features.Students.DTOs;

namespace TrainHub.Application.Interfaces;

/// <summary>
/// Contract for validating Student-related business rules and data integrity.
/// </summary>
public interface IStudentValidator
{
    /// <summary>
    /// Validates business rules before creating a student (e.g. user existence, student role, uniqueness of code, age constraints).
    /// </summary>
    Task ValidateCreateAsync(CreateStudentDto dto, Guid organizationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates business rules before updating a student (e.g. existence, code uniqueness within tenant, age constraints).
    /// </summary>
    Task ValidateUpdateAsync(Guid id, UpdateStudentDto dto, Guid organizationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates business rules before deleting a student (e.g. existence, ensuring no dependent enrollments or attendance records).
    /// </summary>
    Task ValidateDeleteAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates query, filtering, search, sorting, and pagination parameters.
    /// </summary>
    void ValidateQuery(StudentQueryParameters query);
}
