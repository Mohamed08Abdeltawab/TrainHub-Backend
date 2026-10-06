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
    /// Retrieves a paginated list of attendance records matching the query criteria within the current organization, filtered by caller permissions.
    /// </summary>
    Task<PagedResult<AttendanceDto>> GetPagedAsync(AttendanceQueryParameters parameters, Guid? currentUserId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves an attendance record by its ID within the current organization.
    /// </summary>
    Task<AttendanceDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves an attendance record by its ID within the current organization, verifying caller authorization.
    /// </summary>
    Task<AttendanceDto> GetByIdAsync(Guid id, Guid? currentUserId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all attendance records for a specific session within the current organization.
    /// </summary>
    Task<IReadOnlyList<AttendanceDto>> GetBySessionIdAsync(Guid sessionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all attendance records for a specific session within the current organization, verifying caller authorization.
    /// </summary>
    Task<IReadOnlyList<AttendanceDto>> GetBySessionIdAsync(Guid sessionId, Guid? currentUserId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Calculates attendance percentage and breakdown for a student, optionally filtered by course group, verifying caller authorization.
    /// </summary>
    Task<AttendancePercentageDto> GetPercentageAsync(Guid studentId, Guid? courseGroupId = null, Guid? currentUserId = null, CancellationToken cancellationToken = default);

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
