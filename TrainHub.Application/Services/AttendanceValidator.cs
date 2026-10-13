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

    public async Task<CallerPermissions> ResolveCallerPermissionsAsync(
        Guid userId,
        Guid organizationId,
        CancellationToken cancellationToken = default)
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
                    return new CallerPermissions(true, null, null);
                }
            }
        }

        var instructor = await _dbContext.Instructors
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(i => i.UserId == userId && i.OrganizationId == organizationId, cancellationToken);

        if (instructor != null)
        {
            return new CallerPermissions(false, instructor, null);
        }

        var student = await _dbContext.Students
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.UserId == userId && s.OrganizationId == organizationId, cancellationToken);

        if (student != null)
        {
            return new CallerPermissions(false, null, student);
        }

        return new CallerPermissions(false, null, null);
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

        // 2. Prevent recording attendance for cancelled or draft sessions
        if (session.Status == SessionConstants.Statuses.Cancelled)
        {
            _logger.LogWarning("Validation failed: Attempted to record attendance for cancelled session '{SessionId}'.", dto.SessionId);
            throw new ConflictException("Cannot record attendance for a cancelled session.");
        }

        if (session.Status == SessionConstants.Statuses.Draft)
        {
            _logger.LogWarning("Validation failed: Attempted to record attendance for draft session '{SessionId}'.", dto.SessionId);
            throw new ConflictException("Cannot record attendance for a draft session.");
        }

        // 3. Prevent recording attendance if the session's course group is cancelled
        if (session.CourseGroup != null && session.CourseGroup.Status == CourseGroupConstants.Statuses.Cancelled)
        {
            _logger.LogWarning("Validation failed: Attempted to record attendance for session in cancelled course group '{CourseGroupId}'.", session.CourseGroupId);
            throw new ConflictException("Cannot record attendance for a session in a cancelled course group.");
        }

        // 4. Verify Student exists within the current organization
        var student = await _dbContext.Students
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.Id == dto.StudentId && s.OrganizationId == organizationId, cancellationToken);

        if (student == null)
        {
            _logger.LogWarning("Validation failed: Student '{StudentId}' not found in organization '{OrganizationId}'.",
                dto.StudentId, organizationId);
            throw new NotFoundException("Student", dto.StudentId);
        }

        // 5. Verify Student user account is active
        var studentUser = await _dbContext.ApplicationUsers
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Id == student.UserId, cancellationToken);

        if (studentUser != null && !studentUser.IsActive)
        {
            _logger.LogWarning("Validation failed: Student '{StudentId}' has an inactive user account.", dto.StudentId);
            throw new ConflictException("Cannot record attendance for a student whose user account is inactive.");
        }

        // 6. Verify Student is actively enrolled in the session's CourseGroup
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

        if (enrollment.Status == EnrollmentConstants.Statuses.Pending)
        {
            _logger.LogWarning("Validation failed: Student '{StudentId}' has pending enrollment in CourseGroup '{CourseGroupId}'.",
                dto.StudentId, session.CourseGroupId);
            throw new ConflictException("Cannot record attendance for a student whose enrollment is pending.");
        }

        // 7. Prevent duplicate attendance records for the same Student and Session
        var duplicateExists = await _dbContext.AttendanceRecords
            .IgnoreQueryFilters()
            .AnyAsync(a => a.SessionId == dto.SessionId && a.StudentId == dto.StudentId && a.OrganizationId == organizationId, cancellationToken);

        if (duplicateExists)
        {
            _logger.LogWarning("Validation failed: Attendance record already exists for student '{StudentId}' in session '{SessionId}'.",
                dto.StudentId, dto.SessionId);
            throw new ConflictException("An attendance record already exists for this student in this session.");
        }

        // 8. Instructor authorization check
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

        // 3. Prevent updating attendance if course group is cancelled
        if (record.Session.CourseGroup != null && record.Session.CourseGroup.Status == CourseGroupConstants.Statuses.Cancelled)
        {
            _logger.LogWarning("Validation failed: Attempted to update attendance for cancelled course group '{CourseGroupId}'.", record.Session.CourseGroupId);
            throw new ConflictException("Cannot update attendance for a session in a cancelled course group.");
        }

        // 4. Prevent updating attendance for a student whose enrollment is cancelled
        var enrollment = await _dbContext.Enrollments
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(e => e.CourseGroupId == record.Session.CourseGroupId && e.StudentId == record.StudentId && e.OrganizationId == organizationId, cancellationToken);

        if (enrollment != null && enrollment.Status == EnrollmentConstants.Statuses.Cancelled)
        {
            _logger.LogWarning("Validation failed: Student '{StudentId}' has cancelled enrollment in CourseGroup '{CourseGroupId}'.",
                record.StudentId, record.Session.CourseGroupId);
            throw new ConflictException("Cannot update attendance for a student whose enrollment is cancelled.");
        }

        // 5. Instructor authorization check
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

        // 3. Prevent updating status if course group is cancelled
        if (record.Session.CourseGroup != null && record.Session.CourseGroup.Status == CourseGroupConstants.Statuses.Cancelled)
        {
            _logger.LogWarning("Validation failed: Attempted to update attendance status for cancelled course group '{CourseGroupId}'.", record.Session.CourseGroupId);
            throw new ConflictException("Cannot update attendance status for a session in a cancelled course group.");
        }

        // 4. Prevent updating attendance status for a student whose enrollment is cancelled
        var enrollment = await _dbContext.Enrollments
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(e => e.CourseGroupId == record.Session.CourseGroupId && e.StudentId == record.StudentId && e.OrganizationId == organizationId, cancellationToken);

        if (enrollment != null && enrollment.Status == EnrollmentConstants.Statuses.Cancelled)
        {
            _logger.LogWarning("Validation failed: Student '{StudentId}' has cancelled enrollment in CourseGroup '{CourseGroupId}'.",
                record.StudentId, record.Session.CourseGroupId);
            throw new ConflictException("Cannot update attendance status for a student whose enrollment is cancelled.");
        }

        // 5. Instructor authorization check
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

        // 2. Prevent deleting attendance for cancelled sessions
        if (record.Session.Status == SessionConstants.Statuses.Cancelled)
        {
            _logger.LogWarning("Validation failed: Attempted to delete attendance for cancelled session '{SessionId}'.", record.SessionId);
            throw new ConflictException("Cannot delete attendance for a cancelled session.");
        }

        // 3. Instructor authorization check
        await ValidateInstructorAuthorizationForSessionAsync(record.Session, currentUserId, organizationId, cancellationToken);
    }

    public async Task ValidateGetByIdAsync(Guid id, Guid organizationId, Guid? currentUserId = null, CancellationToken cancellationToken = default)
    {
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

        if (!currentUserId.HasValue)
        {
            return;
        }

        var caller = await ResolveCallerPermissionsAsync(currentUserId.Value, organizationId, cancellationToken);
        if (caller.IsOrgStaff)
        {
            return;
        }

        if (caller.Student != null)
        {
            if (record.StudentId != caller.Student.Id)
            {
                _logger.LogWarning("Student '{StudentId}' attempted to access attendance record '{RecordId}' belonging to student '{TargetStudentId}'.",
                    caller.Student.Id, id, record.StudentId);
                throw new ForbiddenAccessException("Students can only view their own attendance records.");
            }
            return;
        }

        if (caller.Instructor != null)
        {
            var isSessionInstructor = record.Session.InstructorId == caller.Instructor.Id;
            var isGroupInstructor = record.Session.CourseGroup != null && record.Session.CourseGroup.InstructorId == caller.Instructor.Id;

            if (!isSessionInstructor && !isGroupInstructor)
            {
                _logger.LogWarning("Instructor '{InstructorId}' attempted to view attendance record '{RecordId}' for session '{SessionId}' which they do not teach.",
                    caller.Instructor.Id, id, record.SessionId);
                throw new ForbiddenAccessException("You are not authorized to view attendance for this session as you are not the instructor for this session or its course group.");
            }
            return;
        }

        throw new ForbiddenAccessException("You are not authorized to view this attendance record.");
    }

    public async Task ValidateGetBySessionIdAsync(Guid sessionId, Guid organizationId, Guid? currentUserId = null, CancellationToken cancellationToken = default)
    {
        var session = await _dbContext.Sessions
            .IgnoreQueryFilters()
            .Include(s => s.CourseGroup)
            .FirstOrDefaultAsync(s => s.Id == sessionId && s.OrganizationId == organizationId, cancellationToken);

        if (session == null)
        {
            _logger.LogWarning("Validation failed: Session '{SessionId}' was not found in organization '{OrganizationId}'.", sessionId, organizationId);
            throw new NotFoundException("Session", sessionId);
        }

        if (!currentUserId.HasValue)
        {
            return;
        }

        var caller = await ResolveCallerPermissionsAsync(currentUserId.Value, organizationId, cancellationToken);
        if (caller.IsOrgStaff)
        {
            return;
        }

        if (caller.Student != null)
        {
            _logger.LogWarning("Student '{StudentId}' attempted to access session roster for session '{SessionId}'.", caller.Student.Id, sessionId);
            throw new ForbiddenAccessException("Students cannot view session attendance rosters.");
        }

        if (caller.Instructor != null)
        {
            var isSessionInstructor = session.InstructorId == caller.Instructor.Id;
            var isGroupInstructor = session.CourseGroup != null && session.CourseGroup.InstructorId == caller.Instructor.Id;

            if (!isSessionInstructor && !isGroupInstructor)
            {
                _logger.LogWarning("Instructor '{InstructorId}' attempted to view roster for session '{SessionId}' which they do not teach.",
                    caller.Instructor.Id, sessionId);
                throw new ForbiddenAccessException("You are not authorized to view attendance for this session as you are not the instructor for this session or its course group.");
            }
            return;
        }

        throw new ForbiddenAccessException("You are not authorized to view attendance for this session.");
    }

    public async Task ValidateGetPercentageAsync(Guid studentId, Guid? courseGroupId, Guid organizationId, Guid? currentUserId = null, CancellationToken cancellationToken = default)
    {
        // 1. Student must exist in organization
        var student = await _dbContext.Students
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => (s.Id == studentId || s.UserId == studentId) && s.OrganizationId == organizationId, cancellationToken);

        if (student == null)
        {
            _logger.LogWarning("Validation failed: Student '{StudentId}' not found in organization '{OrganizationId}'.", studentId, organizationId);
            throw new NotFoundException("Student", studentId);
        }

        // 2. If courseGroupId is provided, verify it exists in organization and student is enrolled
        CourseGroup? courseGroup = null;
        if (courseGroupId.HasValue)
        {
            courseGroup = await _dbContext.CourseGroups
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(cg => cg.Id == courseGroupId.Value && cg.OrganizationId == organizationId, cancellationToken);

            if (courseGroup == null)
            {
                _logger.LogWarning("Validation failed: Course group '{CourseGroupId}' not found in organization '{OrganizationId}'.", courseGroupId.Value, organizationId);
                throw new NotFoundException("CourseGroup", courseGroupId.Value);
            }

            var isEnrolled = await _dbContext.Enrollments
                .IgnoreQueryFilters()
                .AnyAsync(e => e.CourseGroupId == courseGroupId.Value && e.StudentId == studentId && e.OrganizationId == organizationId, cancellationToken);

            if (!isEnrolled)
            {
                _logger.LogWarning("Validation failed: Student '{StudentId}' is not enrolled in course group '{CourseGroupId}'.", studentId, courseGroupId.Value);
                throw new ConflictException("Student is not enrolled in the specified course group.");
            }
        }

        if (!currentUserId.HasValue)
        {
            return;
        }

        var caller = await ResolveCallerPermissionsAsync(currentUserId.Value, organizationId, cancellationToken);
        if (caller.IsOrgStaff)
        {
            return;
        }

        if (caller.Student != null)
        {
            if (student.Id != caller.Student.Id && student.UserId != caller.Student.UserId)
            {
                _logger.LogWarning("Student '{StudentId}' attempted to view attendance percentage of student '{TargetStudentId}'.",
                    caller.Student.Id, studentId);
                throw new ForbiddenAccessException("Students can only view their own attendance data.");
            }
            return;
        }

        if (caller.Instructor != null)
        {
            if (courseGroupId.HasValue)
            {
                var isGroupInstructor = courseGroup != null && courseGroup.InstructorId == caller.Instructor.Id;
                var teachesSessionInGroup = await _dbContext.Sessions
                    .IgnoreQueryFilters()
                    .AnyAsync(s => s.CourseGroupId == courseGroupId.Value && s.InstructorId == caller.Instructor.Id, cancellationToken);

                if (!isGroupInstructor && !teachesSessionInGroup)
                {
                    _logger.LogWarning("Instructor '{InstructorId}' attempted to view student percentage for course group '{CourseGroupId}' which they do not teach.",
                        caller.Instructor.Id, courseGroupId.Value);
                    throw new ForbiddenAccessException("You are not authorized to view attendance data for this course group.");
                }
            }
            else
            {
                // Instructor must teach at least one course group or session in which the student is enrolled
                var teachesStudent = await _dbContext.Enrollments
                    .IgnoreQueryFilters()
                    .Include(e => e.CourseGroup)
                    .AnyAsync(e => e.StudentId == studentId && e.OrganizationId == organizationId &&
                        (e.CourseGroup.InstructorId == caller.Instructor.Id ||
                         _dbContext.Sessions.Any(s => s.CourseGroupId == e.CourseGroupId && s.InstructorId == caller.Instructor.Id)),
                        cancellationToken);

                if (!teachesStudent)
                {
                    _logger.LogWarning("Instructor '{InstructorId}' attempted to view attendance percentage for student '{StudentId}' whom they do not teach.",
                        caller.Instructor.Id, studentId);
                    throw new ForbiddenAccessException("You are not authorized to view attendance data for this student.");
                }
            }
            return;
        }

        throw new ForbiddenAccessException("You are not authorized to view attendance data for this student.");
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

        var caller = await ResolveCallerPermissionsAsync(currentUserId.Value, organizationId, cancellationToken);

        if (caller.IsOrgStaff)
        {
            return;
        }

        if (caller.Instructor == null)
        {
            _logger.LogWarning("User '{UserId}' is neither OrgStaff nor a registered instructor in organization '{OrganizationId}'.",
                currentUserId.Value, organizationId);
            throw new ForbiddenAccessException("You are not authorized to manage attendance for this session.");
        }

        var isSessionInstructor = session.InstructorId == caller.Instructor.Id;
        var isGroupInstructor = session.CourseGroup != null && session.CourseGroup.InstructorId == caller.Instructor.Id;

        if (!isSessionInstructor && !isGroupInstructor)
        {
            _logger.LogWarning("Instructor '{InstructorId}' attempted to manage attendance for session '{SessionId}' which they do not teach.",
                caller.Instructor.Id, session.Id);
            throw new ForbiddenAccessException("You are not authorized to manage attendance for this session as you are not the instructor for this session or its course group.");
        }
    }

    #endregion
}
