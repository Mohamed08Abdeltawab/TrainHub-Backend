using TrainHub.Application.Features.Enrollments.DTOs;

namespace TrainHub.Application.Interfaces;

/// <summary>
/// Contract for validating Enrollment operations and business rules.
/// </summary>
public interface IEnrollmentValidator
{
    /// <summary>
    /// Validates pagination, filtering, and sorting parameters for querying enrollments.
    /// </summary>
    void ValidateQuery(EnrollmentQueryParameters query);

    /// <summary>
    /// Validates business invariants and existence checks before creating an enrollment.
    /// </summary>
    Task ValidateCreateAsync(CreateEnrollmentDto dto, Guid organizationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates business invariants and existence checks before updating an enrollment.
    /// </summary>
    Task ValidateUpdateAsync(Guid id, UpdateEnrollmentDto dto, Guid organizationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates business invariants before updating the status of an enrollment.
    /// </summary>
    Task ValidateUpdateStatusAsync(Guid id, byte status, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates that an enrollment can be safely deleted.
    /// </summary>
    Task ValidateDeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
