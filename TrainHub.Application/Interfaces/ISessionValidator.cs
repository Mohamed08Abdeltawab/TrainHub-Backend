using TrainHub.Application.Features.Sessions.DTOs;

namespace TrainHub.Application.Interfaces;

/// <summary>
/// Contract for validating Session operations, scheduling rules, and instructor authorization.
/// </summary>
public interface ISessionValidator
{
    /// <summary>
    /// Validates pagination, filtering, and sorting parameters for querying sessions.
    /// </summary>
    void ValidateQuery(SessionQueryParameters query);

    /// <summary>
    /// Validates business invariants, CourseGroup scheduling boundaries, existence checks,
    /// and instructor authorization before creating/scheduling a session.
    /// </summary>
    Task ValidateCreateAsync(CreateSessionDto dto, Guid organizationId, Guid? currentUserId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates business invariants, scheduling constraints, existence checks,
    /// and instructor authorization before updating a session.
    /// </summary>
    Task ValidateUpdateAsync(Guid id, UpdateSessionDto dto, Guid organizationId, Guid? currentUserId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates scheduling constraints, CourseGroup date boundaries, and instructor authorization before rescheduling a session.
    /// </summary>
    Task ValidateRescheduleAsync(Guid id, RescheduleSessionDto dto, Guid organizationId, Guid? currentUserId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates business invariants and instructor authorization before updating the status of a session.
    /// </summary>
    Task ValidateUpdateStatusAsync(Guid id, byte status, Guid organizationId, Guid? currentUserId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates that a session exists, the caller is authorized, and no dependent records prevent deletion.
    /// </summary>
    Task ValidateDeleteAsync(Guid id, Guid organizationId, Guid? currentUserId = null, CancellationToken cancellationToken = default);
}
