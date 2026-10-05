using TrainHub.Application.Features.Attendances.DTOs;

namespace TrainHub.Application.Interfaces;

/// <summary>
/// Contract for validating Attendance operations, relational integrity, business rules, and authorization.
/// </summary>
public interface IAttendanceValidator
{
    /// <summary>
    /// Validates pagination, filtering, and sorting parameters for querying attendance records.
    /// </summary>
    void ValidateQuery(AttendanceQueryParameters query);

    /// <summary>
    /// Validates business invariants, student enrollment, duplicate prevention, and instructor authorization before creating an attendance record.
    /// </summary>
    Task ValidateCreateAsync(CreateAttendanceDto dto, Guid organizationId, Guid? currentUserId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates business invariants, existence checks, session status, and instructor authorization before updating an attendance record.
    /// </summary>
    Task ValidateUpdateAsync(Guid id, UpdateAttendanceDto dto, Guid organizationId, Guid? currentUserId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates business invariants and instructor authorization before updating the status of an attendance record.
    /// </summary>
    Task ValidateUpdateStatusAsync(Guid id, byte status, Guid organizationId, Guid? currentUserId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates that an attendance record exists and the caller is authorized before deleting it.
    /// </summary>
    Task ValidateDeleteAsync(Guid id, Guid organizationId, Guid? currentUserId = null, CancellationToken cancellationToken = default);
}
