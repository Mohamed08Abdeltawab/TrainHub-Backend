using TrainHub.Application.Features.Attendances.DTOs;
using TrainHub.Domain.Models;

namespace TrainHub.Application.Interfaces;

/// <summary>
/// Holds resolved permissions and associated domain profiles for the current caller in an organization context.
/// </summary>
public record CallerPermissions(bool IsOrgStaff, Instructor? Instructor, Student? Student);

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
    /// Resolves caller permissions (OrgStaff, Instructor, Student) within the given organization.
    /// </summary>
    Task<CallerPermissions> ResolveCallerPermissionsAsync(Guid userId, Guid organizationId, CancellationToken cancellationToken = default);

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
    /// Validates that an attendance record exists, session is not cancelled, and the caller is authorized before deleting it.
    /// </summary>
    Task ValidateDeleteAsync(Guid id, Guid organizationId, Guid? currentUserId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates that an attendance record exists within the organization and the caller is authorized to view it.
    /// </summary>
    Task ValidateGetByIdAsync(Guid id, Guid organizationId, Guid? currentUserId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates that a session exists within the organization and the caller is authorized to view its attendance roster.
    /// </summary>
    Task ValidateGetBySessionIdAsync(Guid sessionId, Guid organizationId, Guid? currentUserId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates parameters, entity existence, enrollment, and caller authorization for calculating attendance percentage.
    /// </summary>
    Task ValidateGetPercentageAsync(Guid studentId, Guid? courseGroupId, Guid organizationId, Guid? currentUserId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates business invariants, student eligibility, duplicate prevention, and caller authorization for a batch attendance submission.
    /// </summary>
    Task ValidateBatchAsync(Guid sessionId, BatchAttendanceRequestDto dto, Guid organizationId, Guid? currentUserId = null, CancellationToken cancellationToken = default);
}
