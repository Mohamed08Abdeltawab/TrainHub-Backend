using TrainHub.Application.Common.Models;
using TrainHub.Application.Features.Attendances.DTOs;

namespace TrainHub.Application.Interfaces;

/// <summary>
/// Service contract for managing Attendance entities, attendance marking operations, and authorization.
/// </summary>
public interface IAttendanceService
{
    /// <summary>
    /// Retrieves a paginated list of attendance records matching the query criteria within the current organization.
    /// </summary>
    Task<PagedResult<AttendanceDto>> GetPagedAsync(AttendanceQueryParameters parameters, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves an attendance record by its ID within the current organization.
    /// </summary>
    Task<AttendanceDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all attendance records for a specific session within the current organization.
    /// </summary>
    Task<IReadOnlyList<AttendanceDto>> GetBySessionIdAsync(Guid sessionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records attendance for a student in a session within the current organization.
    /// </summary>
    Task<AttendanceDto> CreateAsync(CreateAttendanceDto dto, Guid? currentUserId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates details of an existing attendance record.
    /// </summary>
    Task<AttendanceDto> UpdateAsync(Guid id, UpdateAttendanceDto dto, Guid? currentUserId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the status of an existing attendance record.
    /// </summary>
    Task<AttendanceDto> UpdateStatusAsync(Guid id, UpdateAttendanceStatusDto dto, Guid? currentUserId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes an attendance record.
    /// </summary>
    Task DeleteAsync(Guid id, Guid? currentUserId = null, CancellationToken cancellationToken = default);
}
