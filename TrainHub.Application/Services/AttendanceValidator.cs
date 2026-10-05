using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TrainHub.Application.Common.Exceptions;
using TrainHub.Application.Features.Attendances.DTOs;
using TrainHub.Application.Interfaces;
using TrainHub.Domain.Constants;
using TrainHub.Domain.Models;

namespace TrainHub.Application.Services;

/// <summary>
/// Validates business rules, constraints, relational integrity, and authorization for Attendance records.
/// </summary>
public class AttendanceValidator : IAttendanceValidator
{
    private readonly ITrainHubDbContext _dbContext;
    private readonly ILogger<AttendanceValidator> _logger;
    private readonly UserManager<ApplicationUser>? _userManager;

    public AttendanceValidator(
        ITrainHubDbContext dbContext,
        ILogger<AttendanceValidator> logger,
        UserManager<ApplicationUser>? userManager = null)
    {
        _dbContext = dbContext;
        _logger = logger;
        _userManager = userManager;
    }

    public void ValidateQuery(AttendanceQueryParameters query)
    {
        ArgumentNullException.ThrowIfNull(query);

        var errors = new Dictionary<string, string[]>();

        if (query.PageNumber < 1)
        {
            errors[nameof(query.PageNumber)] = ["Page number must be at least 1."];
        }

        if (query.PageSize < 1 || query.PageSize > AttendanceConstants.MaxPageSize)
        {
            errors[nameof(query.PageSize)] = [$"Page size must be between 1 and {AttendanceConstants.MaxPageSize}."];
        }

        if (query.FromDate.HasValue && query.ToDate.HasValue && query.FromDate.Value > query.ToDate.Value)
        {
            errors[nameof(query.FromDate)] = ["'FromDate' cannot be later than 'ToDate'."];
        }

        if (query.Status.HasValue && !AttendanceConstants.IsValidStatus(query.Status.Value))
        {
            errors[nameof(query.Status)] = [$"Invalid status '{query.Status.Value}'. Allowed values: {string.Join(", ", AttendanceConstants.Statuses.All)}."];
        }

        if (!string.IsNullOrWhiteSpace(query.SortBy) && !AttendanceConstants.IsValidSortField(query.SortBy))
        {
            errors[nameof(query.SortBy)] = [$"Invalid sort field '{query.SortBy}'. Allowed values: {string.Join(", ", AttendanceConstants.AllowedSortFields)}."];
        }

        if (errors.Count > 0)
        {
            _logger.LogWarning("Validation failed for attendance query parameters: {ErrorCount} error(s).", errors.Count);
            throw new ValidationException(errors);
        }
    }

    public async Task ValidateCreateAsync(CreateAttendanceDto dto, Guid organizationId, Guid? currentUserId = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        if (!AttendanceConstants.IsValidStatus(dto.Status))
        {
            _logger.LogWarning("Validation failed: Invalid attendance status '{Status}'.", dto.Status);
            throw new ValidationException(nameof(dto.Status),
                $"Invalid status '{dto.Status}'. Allowed values: {string.Join(", ", AttendanceConstants.Statuses.All)}.");
        }

        if (!AttendanceConstants.IsValidNotes(dto.Notes))
        {
            _logger.LogWarning("Validation failed: Notes exceed max length.");
            throw new ValidationException(nameof(dto.Notes),
                $"Notes exceeds maximum length of {AttendanceConstants.NotesMaxLength} characters.");
        }

        // 1. Verify Session exists within the current organization
        var session = await _dbContext.Sessions
            .IgnoreQueryFilters()
            .Include(s => s.CourseGroup)
            .FirstOrDefaultAsync(s => s.Id == dto.SessionId && s.OrganizationId == organizationId, cancellationToken);

        if (session == null)
        {
            _logger.LogWarning("Validation failed: Session '{SessionId}' not found in organization '{OrganizationId}'.",
                dto.SessionId, organizationId);
            throw new NotFoundException("Session", dto.SessionId);
        }

        // 2. Prevent recording attendance for cancelled sessions
        if (session.Status == SessionConstants.Statuses.Cancelled)
        {
            _logger.LogWarning("Validation failed: Attempted to record attendance for cancelled session '{SessionId}'.", dto.SessionId);
            throw new ConflictException("Cannot record attendance for a cancelled session.");
        }

        // 3. Verify Student exists within the current organization
        var student = await _dbContext.Students
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.Id == dto.StudentId && s.OrganizationId == organizationId, cancellationToken);

        if (student == null)
        {
            _logger.LogWarning("Validation failed: Student '{StudentId}' not found in organization '{OrganizationId}'.",
                dto.StudentId, organizationId);
            throw new NotFoundException("Student", dto.StudentId);
        }

        // 4. Verify Student is enrolled in the session's CourseGroup
        var enrollment = await _dbContext.Enrollments
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(e => e.CourseGroupId == session.CourseGroupId && e.StudentId == dto.StudentId && e.OrganizationId == organizationId, cancellationToken);

        if (enrollment == null)
        {
            _logger.LogWarning("Validation failed: Student '{StudentId}' is not enrolled in CourseGroup '{CourseGroupId}'.",
                dto.StudentId, session.CourseGroupId);
            throw new ConflictException("Student is not enrolled in the course group for this session.");
        }

        if (enrollment.Status == EnrollmentConstants.Statuses.Cancelled)
        {
            _logger.LogWarning("Validation failed: Student '{StudentId}' has cancelled enrollment in CourseGroup '{CourseGroupId}'.",
                dto.StudentId, session.CourseGroupId);
            throw new ConflictException("Cannot record attendance for a student whose enrollment is cancelled.");
        }

        // 5. Prevent duplicate attendance records for the same Student and Session
        var duplicateExists = await _dbContext.AttendanceRecords
            .IgnoreQueryFilters()
            .AnyAsync(a => a.SessionId == dto.SessionId && a.StudentId == dto.StudentId && a.OrganizationId == organizationId, cancellationToken);

        if (duplicateExists)
        {
            _logger.LogWarning("Validation failed: Attendance record already exists for student '{StudentId}' in session '{SessionId}'.",
                dto.StudentId, dto.SessionId);
            throw new ConflictException("An attendance record already exists for this student in this session.");
        }

        // 6. Instructor authorization check
        await ValidateInstructorAuthorizationForSessionAsync(session, currentUserId, organizationId, cancellationToken);
    }

    public async Task ValidateUpdateAsync(Guid id, UpdateAttendanceDto dto, Guid organizationId, Guid? currentUserId = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        if (dto.Status.HasValue && !AttendanceConstants.IsValidStatus(dto.Status.Value))
        {
            _logger.LogWarning("Validation failed: Invalid attendance status '{Status}'.", dto.Status.Value);
            throw new ValidationException(nameof(dto.Status),
                $"Invalid status '{dto.Status.Value}'. Allowed values: {string.Join(", ", AttendanceConstants.Statuses.All)}.");
        }

        if (dto.Notes != null && !AttendanceConstants.IsValidNotes(dto.Notes))
        {
            _logger.LogWarning("Validation failed: Notes exceed max length.");
            throw new ValidationException(nameof(dto.Notes),
                $"Notes exceeds maximum length of {AttendanceConstants.NotesMaxLength} characters.");
        }

        // 1. Verify Attendance record exists within the current organization
        var record = await _dbContext.AttendanceRecords
            .IgnoreQueryFilters()
            .Include(a => a.Session)
                .ThenInclude(s => s.CourseGroup)
            .FirstOrDefaultAsync(a => a.Id == id && a.OrganizationId == organizationId, cancellationToken);

        if (record == null)
        {
            _logger.LogWarning("Validation failed: AttendanceRecord '{Id}' not found in organization '{OrganizationId}'.", id, organizationId);
            throw new NotFoundException("AttendanceRecord", id);
        }

        // 2. Prevent updating attendance for cancelled sessions
        if (record.Session.Status == SessionConstants.Statuses.Cancelled)
        {
            _logger.LogWarning("Validation failed: Attempted to update attendance for cancelled session '{SessionId}'.", record.SessionId);
            throw new ConflictException("Cannot update attendance for a cancelled session.");
        }

        // 3. Instructor authorization check
        await ValidateInstructorAuthorizationForSessionAsync(record.Session, currentUserId, organizationId, cancellationToken);
    }

    public async Task ValidateUpdateStatusAsync(Guid id, byte status, Guid organizationId, Guid? currentUserId = null, CancellationToken cancellationToken = default)
    {
        if (!AttendanceConstants.IsValidStatus(status))
        {
            _logger.LogWarning("Validation failed: Invalid attendance status '{Status}'.", status);
            throw new ValidationException(nameof(status),
                $"Invalid status '{status}'. Allowed values: {string.Join(", ", AttendanceConstants.Statuses.All)}.");
        }

        // 1. Verify Attendance record exists within organization
        var record = await _dbContext.AttendanceRecords
            .IgnoreQueryFilters()
            .Include(a => a.Session)
                .ThenInclude(s => s.CourseGroup)
            .FirstOrDefaultAsync(a => a.Id == id && a.OrganizationId == organizationId, cancellationToken);

        if (record == null)
        {
            _logger.LogWarning("Validation failed: AttendanceRecord '{Id}' not found in organization '{OrganizationId}'.", id, organizationId);
            throw new NotFoundException("AttendanceRecord", id);
        }

        // 2. Prevent updating attendance status for cancelled sessions
        if (record.Session.Status == SessionConstants.Statuses.Cancelled)
        {
            _logger.LogWarning("Validation failed: Attempted to update status for cancelled session '{SessionId}'.", record.SessionId);
            throw new ConflictException("Cannot update attendance status for a cancelled session.");
        }

        // 3. Instructor authorization check
        await ValidateInstructorAuthorizationForSessionAsync(record.Session, currentUserId, organizationId, cancellationToken);
    }

    public async Task ValidateDeleteAsync(Guid id, Guid organizationId, Guid? currentUserId = null, CancellationToken cancellationToken = default)
    {
        // 1. Verify Attendance record exists within organization
        var record = await _dbContext.AttendanceRecords
            .IgnoreQueryFilters()
            .Include(a => a.Session)
                .ThenInclude(s => s.CourseGroup)
            .FirstOrDefaultAsync(a => a.Id == id && a.OrganizationId == organizationId, cancellationToken);

        if (record == null)
        {
            _logger.LogWarning("Validation failed: AttendanceRecord '{Id}' not found for deletion.", id);
            throw new NotFoundException("AttendanceRecord", id);
        }

        // 2. Instructor authorization check
        await ValidateInstructorAuthorizationForSessionAsync(record.Session, currentUserId, organizationId, cancellationToken);
    }

    #region Helper Methods

    private async Task ValidateInstructorAuthorizationForSessionAsync(
        Session session,
        Guid? currentUserId,
        Guid organizationId,
        CancellationToken cancellationToken)
    {
        if (!currentUserId.HasValue)
        {
            return;
        }

        var (isOrgStaff, instructor) = await ResolveCallerPermissionsAsync(currentUserId.Value, organizationId, cancellationToken);

        if (isOrgStaff)
        {
            return;
        }

        if (instructor == null)
        {
            _logger.LogWarning("User '{UserId}' is neither OrgStaff nor a registered instructor in organization '{OrganizationId}'.",
                currentUserId.Value, organizationId);
            throw new ForbiddenAccessException("You are not authorized to manage attendance for this session.");
        }

        var isSessionInstructor = session.InstructorId == instructor.Id;
        var isGroupInstructor = session.CourseGroup != null && session.CourseGroup.InstructorId == instructor.Id;

        if (!isSessionInstructor && !isGroupInstructor)
        {
            _logger.LogWarning("Instructor '{InstructorId}' attempted to manage attendance for session '{SessionId}' which they do not teach.",
                instructor.Id, session.Id);
            throw new ForbiddenAccessException("You are not authorized to manage attendance for this session as you are not the instructor for this session or its course group.");
        }
    }

    private async Task<(bool IsOrgStaff, Instructor? Instructor)> ResolveCallerPermissionsAsync(
        Guid userId,
        Guid organizationId,
        CancellationToken cancellationToken)
    {
        if (_userManager != null)
        {
            var user = await _userManager.FindByIdAsync(userId.ToString());
            if (user != null)
            {
                var isSuperAdmin = await _userManager.IsInRoleAsync(user, AppRoles.SuperAdmin);
                var isOrgAdmin = await _userManager.IsInRoleAsync(user, AppRoles.OrgAdmin);

                if (isSuperAdmin || isOrgAdmin)
                {
                    return (true, null);
                }
            }
        }

        var instructor = await _dbContext.Instructors
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(i => i.UserId == userId && i.OrganizationId == organizationId, cancellationToken);

        return (false, instructor);
    }

    #endregion
}
