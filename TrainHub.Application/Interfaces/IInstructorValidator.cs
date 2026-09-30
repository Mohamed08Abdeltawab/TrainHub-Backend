using TrainHub.Application.Features.Instructors.DTOs;

namespace TrainHub.Application.Interfaces;

/// <summary>
/// Contract for validating Instructor-related business rules and data integrity.
/// </summary>
public interface IInstructorValidator
{
    /// <summary>
    /// Validates business rules before creating an instructor (e.g. user existence, tenant match, 1-to-1 uniqueness, field constraints).
    /// </summary>
    Task ValidateCreateAsync(CreateInstructorDto dto, Guid organizationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates business rules before updating an instructor (e.g. existence, field constraints).
    /// </summary>
    Task ValidateUpdateAsync(Guid id, UpdateInstructorDto dto, Guid organizationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates business rules before deleting an instructor (e.g. existence, ensuring no dependent course groups or sessions).
    /// </summary>
    Task ValidateDeleteAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates business rules before linking an instructor to a user account (e.g. existence of instructor and user, matching organization, 1-to-1 uniqueness, active user state).
    /// </summary>
    Task ValidateLinkUserAsync(Guid instructorId, Guid newUserId, Guid organizationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates query, filtering, search, sorting, and pagination parameters.
    /// </summary>
    void ValidateQuery(InstructorQueryParameters query);
}
