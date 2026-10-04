using TrainHub.Application.Features.Sessions.DTOs;

namespace TrainHub.Application.Interfaces;

/// <summary>
/// Contract for validating Session operations and business rules.
/// </summary>
public interface ISessionValidator
{
    /// <summary>
    /// Validates pagination, filtering, and sorting parameters for querying sessions.
    /// </summary>
    void ValidateQuery(SessionQueryParameters query);

    /// <summary>
    /// Validates business invariants and existence checks before creating a session.
    /// </summary>
    Task ValidateCreateAsync(CreateSessionDto dto, Guid organizationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates business invariants and existence checks before updating a session.
    /// </summary>
    Task ValidateUpdateAsync(Guid id, UpdateSessionDto dto, Guid organizationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates business invariants before updating the status of a session.
    /// </summary>
    Task ValidateUpdateStatusAsync(Guid id, byte status, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates that a session can be safely deleted.
    /// </summary>
    Task ValidateDeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
