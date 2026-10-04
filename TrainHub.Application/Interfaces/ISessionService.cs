using TrainHub.Application.Common.Models;
using TrainHub.Application.Features.Sessions.DTOs;

namespace TrainHub.Application.Interfaces;

/// <summary>
/// Service contract for managing Session entities, scheduling operations, and instructor authorization.
/// </summary>
public interface ISessionService
{
    /// <summary>
    /// Retrieves a paginated list of sessions matching the query criteria within the current organization.
    /// </summary>
    Task<PagedResult<SessionDto>> GetPagedAsync(SessionQueryParameters parameters, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a session by its ID within the current organization.
    /// </summary>
    Task<SessionDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all sessions for a specific course group within the current organization, ordered chronologically.
    /// </summary>
    Task<IReadOnlyList<SessionDto>> GetByCourseGroupIdAsync(Guid courseGroupId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Schedules a new session within a course group in the current organization.
    /// </summary>
    Task<SessionDto> CreateAsync(CreateSessionDto dto, Guid? currentUserId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates details of an existing session.
    /// </summary>
    Task<SessionDto> UpdateAsync(Guid id, UpdateSessionDto dto, Guid? currentUserId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reschedules an existing session to new start/end times and optional room.
    /// </summary>
    Task<SessionDto> RescheduleAsync(Guid id, RescheduleSessionDto dto, Guid? currentUserId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the status of an existing session.
    /// </summary>
    Task<SessionDto> UpdateStatusAsync(Guid id, UpdateSessionStatusDto dto, Guid? currentUserId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a session record if no dependent attendance records exist.
    /// </summary>
    Task DeleteAsync(Guid id, Guid? currentUserId = null, CancellationToken cancellationToken = default);
}
