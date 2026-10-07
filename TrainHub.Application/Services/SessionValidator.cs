using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TrainHub.Application.Common.Exceptions;
using TrainHub.Application.Features.Sessions.DTOs;
using TrainHub.Application.Interfaces;
using TrainHub.Domain.Constants;
using TrainHub.Domain.Models;

namespace TrainHub.Application.Services;

/// <summary>
/// Validates business rules, constraints, scheduling data, and instructor authorization for Sessions.
/// </summary>
public class SessionValidator : ISessionValidator
{
    private readonly ITrainHubDbContext _dbContext;
    private readonly ILogger<SessionValidator> _logger;
    private readonly UserManager<ApplicationUser>? _userManager;

    public SessionValidator(
        ITrainHubDbContext dbContext,
        ILogger<SessionValidator> logger,
        UserManager<ApplicationUser>? userManager = null)
    {
        _dbContext = dbContext;
        _logger = logger;
        _userManager = userManager;
    }

    public void ValidateQuery(SessionQueryParameters query)
    {
        ArgumentNullException.ThrowIfNull(query);

        var errors = new Dictionary<string, string[]>();

        if (query.PageNumber < 1)
        {
            errors[nameof(query.PageNumber)] = ["Page number must be at least 1."];
        }

        if (query.PageSize < 1 || query.PageSize > SessionConstants.MaxPageSize)
        {
            errors[nameof(query.PageSize)] = [$"Page size must be between 1 and {SessionConstants.MaxPageSize}."];
        }

        if (!string.IsNullOrWhiteSpace(query.SearchTerm) && query.SearchTerm.Trim().Length > SessionConstants.SearchTermMaxLength)
        {
            errors[nameof(query.SearchTerm)] = [$"Search term cannot exceed {SessionConstants.SearchTermMaxLength} characters."];
        }

        if (query.Status.HasValue && !SessionConstants.IsValidStatus(query.Status.Value))
        {
            errors[nameof(query.Status)] = [$"Invalid status '{query.Status.Value}'. Allowed values: {string.Join(", ", SessionConstants.Statuses.All)}."];
        }

        if (!string.IsNullOrWhiteSpace(query.SortBy) && !SessionConstants.IsValidSortField(query.SortBy))
        {
            errors[nameof(query.SortBy)] = [$"Invalid sort field '{query.SortBy}'. Allowed values: {string.Join(", ", SessionConstants.AllowedSortFields)}."];
        }

        if (query.FromDate.HasValue && query.ToDate.HasValue && query.ToDate.Value < query.FromDate.Value)
        {
            errors[nameof(query.ToDate)] = ["ToDate cannot be earlier than FromDate."];
        }

        if (errors.Count > 0)
        {
            _logger.LogWarning("Validation failed for session query parameters: {ErrorCount} errors.", errors.Count);
            throw new ValidationException(errors);
        }
    }

    public async Task ValidateCreateAsync(CreateSessionDto dto, Guid organizationId, Guid? currentUserId = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        if (!SessionConstants.IsValidTimeRange(dto.StartTime, dto.EndTime))
        {
            _logger.LogWarning("Validation failed: EndTime '{EndTime}' is earlier than or equal to StartTime '{StartTime}'.", dto.EndTime, dto.StartTime);
            throw new ValidationException(nameof(dto.EndTime), "EndTime must be later than StartTime.");
        }

        if (!SessionConstants.IsValidRoom(dto.Room))
        {
            _logger.LogWarning("Validation failed: Room exceeds max length.");
            throw new ValidationException(nameof(dto.Room), $"Room exceeds maximum length of {SessionConstants.RoomMaxLength} characters.");
        }

        if (!SessionConstants.IsValidStatus(dto.Status))
        {
            _logger.LogWarning("Validation failed: Invalid status '{Status}'.", dto.Status);
            throw new ValidationException(nameof(dto.Status),
                $"Invalid status '{dto.Status}'. Allowed values: {string.Join(", ", SessionConstants.Statuses.All)}.");
        }

        // 1. Verify Organization exists
        var orgExists = await _dbContext.Organizations
            .IgnoreQueryFilters()
            .AnyAsync(o => o.Id == organizationId, cancellationToken);

        if (!orgExists)
        {
            _logger.LogWarning("Validation failed: Organization '{OrganizationId}' does not exist.", organizationId);
            throw new NotFoundException("Organization", organizationId);
        }

        // 2. Verify CourseGroup exists within the organization (multi-tenancy)
        var courseGroup = await _dbContext.CourseGroups
            .IgnoreQueryFilters()
            .Include(cg => cg.Course)
            .FirstOrDefaultAsync(cg => cg.Id == dto.CourseGroupId && cg.OrganizationId == organizationId, cancellationToken);

        if (courseGroup == null)
        {
            _logger.LogWarning("Validation failed: Course group '{CourseGroupId}' does not exist in organization '{OrganizationId}'.",
                dto.CourseGroupId, organizationId);
            throw new NotFoundException("CourseGroup", dto.CourseGroupId);
        }

        // 3. Verify CourseGroup is Active
        ValidateCourseGroupIsActive(courseGroup);

        // 4. Verify scheduling dates fall within the CourseGroup date range
        ValidateCourseGroupDateBoundary(dto.StartTime, dto.EndTime, courseGroup);

        // 5. Verify Instructor exists within the organization and has active user account if provided
        if (dto.InstructorId.HasValue)
        {
            var instructor = await _dbContext.Instructors
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(i => i.Id == dto.InstructorId.Value && i.OrganizationId == organizationId, cancellationToken);

            if (instructor == null)
            {
                _logger.LogWarning("Validation failed: Instructor '{InstructorId}' does not exist in organization '{OrganizationId}'.",
                    dto.InstructorId.Value, organizationId);
                throw new NotFoundException("Instructor", dto.InstructorId.Value);
            }

            var user = await _dbContext.ApplicationUsers
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(u => u.Id == instructor.UserId, cancellationToken);

            if (user != null && !user.IsActive)
            {
                _logger.LogWarning("Validation failed: Instructor '{InstructorId}' has an inactive user account.", dto.InstructorId.Value);
                throw new ConflictException("Cannot assign an instructor whose user account is inactive.");
            }
        }

        // 6. Instructor authorization check based on CourseGroup/Instructor relationship
        await ValidateInstructorAuthorizationForCourseGroupAsync(courseGroup, currentUserId, organizationId, dto.InstructorId, cancellationToken);
    }

    public async Task ValidateUpdateAsync(Guid id, UpdateSessionDto dto, Guid organizationId, Guid? currentUserId = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        if (!SessionConstants.IsValidTimeRange(dto.StartTime, dto.EndTime))
        {
            _logger.LogWarning("Validation failed: EndTime '{EndTime}' is earlier than or equal to StartTime '{StartTime}'.", dto.EndTime, dto.StartTime);
            throw new ValidationException(nameof(dto.EndTime), "EndTime must be later than StartTime.");
        }

        if (!SessionConstants.IsValidRoom(dto.Room))
        {
            _logger.LogWarning("Validation failed: Room exceeds max length.");
            throw new ValidationException(nameof(dto.Room), $"Room exceeds maximum length of {SessionConstants.RoomMaxLength} characters.");
        }

        if (dto.Status.HasValue && !SessionConstants.IsValidStatus(dto.Status.Value))
        {
            _logger.LogWarning("Validation failed: Invalid status '{Status}'.", dto.Status.Value);
            throw new ValidationException(nameof(dto.Status),
                $"Invalid status '{dto.Status.Value}'. Allowed values: {string.Join(", ", SessionConstants.Statuses.All)}.");
        }

        // 1. Verify Session exists within the organization and through its CourseGroup
        var session = await _dbContext.Sessions
            .IgnoreQueryFilters()
            .Include(s => s.CourseGroup)
                .ThenInclude(cg => cg.Course)
            .FirstOrDefaultAsync(s => s.Id == id && s.OrganizationId == organizationId, cancellationToken);

        if (session == null)
        {
            _logger.LogWarning("Validation failed: Session '{SessionId}' was not found for update in org '{OrganizationId}'.", id, organizationId);
            throw new NotFoundException("Session", id);
        }

        // 2. Prevent invalid session states: cannot modify completed or cancelled sessions
        ValidateSessionNotEndedOrCancelled(session, "modify");

        // 3. Prevent invalid session state: cannot complete future session
        if (dto.Status.HasValue && dto.Status.Value == SessionConstants.Statuses.Completed && dto.StartTime > DateTime.UtcNow)
        {
            throw new ConflictException("Cannot mark a future session as completed.");
        }

        // 4. Verify scheduling dates fall within CourseGroup date range
        if (session.CourseGroup != null)
        {
            ValidateCourseGroupDateBoundary(dto.StartTime, dto.EndTime, session.CourseGroup);
        }

        // 5. Verify Instructor exists within the organization and has active user account if provided
        if (dto.InstructorId.HasValue)
        {
            var instructor = await _dbContext.Instructors
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(i => i.Id == dto.InstructorId.Value && i.OrganizationId == organizationId, cancellationToken);

            if (instructor == null)
            {
                _logger.LogWarning("Validation failed: Instructor '{InstructorId}' does not exist in organization '{OrganizationId}'.",
                    dto.InstructorId.Value, organizationId);
                throw new NotFoundException("Instructor", dto.InstructorId.Value);
            }

            var user = await _dbContext.ApplicationUsers
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(u => u.Id == instructor.UserId, cancellationToken);

            if (user != null && !user.IsActive)
            {
                _logger.LogWarning("Validation failed: Instructor '{InstructorId}' has an inactive user account.", dto.InstructorId.Value);
                throw new ConflictException("Cannot assign an instructor whose user account is inactive.");
            }
        }

        // 6. Instructor authorization check based on CourseGroup/Instructor relationship
        await ValidateInstructorAuthorizationForSessionAsync(session, currentUserId, organizationId, dto.InstructorId, cancellationToken);
    }

    public async Task ValidateRescheduleAsync(Guid id, RescheduleSessionDto dto, Guid organizationId, Guid? currentUserId = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        if (!SessionConstants.IsValidTimeRange(dto.StartTime, dto.EndTime))
        {
            _logger.LogWarning("Validation failed: EndTime '{EndTime}' is earlier than or equal to StartTime '{StartTime}'.", dto.EndTime, dto.StartTime);
            throw new ValidationException(nameof(dto.EndTime), "EndTime must be later than StartTime.");
        }

        if (!SessionConstants.IsValidRoom(dto.Room))
        {
            _logger.LogWarning("Validation failed: Room exceeds max length.");
            throw new ValidationException(nameof(dto.Room), $"Room exceeds maximum length of {SessionConstants.RoomMaxLength} characters.");
        }

        // 1. Verify Session exists within the organization
        var session = await _dbContext.Sessions
            .IgnoreQueryFilters()
            .Include(s => s.CourseGroup)
                .ThenInclude(cg => cg.Course)
            .FirstOrDefaultAsync(s => s.Id == id && s.OrganizationId == organizationId, cancellationToken);

        if (session == null)
        {
            _logger.LogWarning("Validation failed: Session '{SessionId}' was not found for rescheduling.", id);
            throw new NotFoundException("Session", id);
        }

        // 2. Prevent invalid session states: cannot reschedule completed or cancelled sessions
        ValidateSessionNotEndedOrCancelled(session, "reschedule");

        // 3. Verify CourseGroup is active
        if (session.CourseGroup != null)
        {
            ValidateCourseGroupIsActive(session.CourseGroup);
            ValidateCourseGroupDateBoundary(dto.StartTime, dto.EndTime, session.CourseGroup);
        }

        // 4. Instructor authorization check based on CourseGroup/Instructor relationship
        await ValidateInstructorAuthorizationForSessionAsync(session, currentUserId, organizationId, null, cancellationToken);
    }

    public async Task ValidateUpdateStatusAsync(Guid id, byte status, Guid organizationId, Guid? currentUserId = null, CancellationToken cancellationToken = default)
    {
        if (!SessionConstants.IsValidStatus(status))
        {
            _logger.LogWarning("Validation failed: Invalid status '{Status}'.", status);
            throw new ValidationException(nameof(status),
                $"Invalid status '{status}'. Allowed values: {string.Join(", ", SessionConstants.Statuses.All)}.");
        }

        // 1. Verify Session exists within organization
        var session = await _dbContext.Sessions
            .IgnoreQueryFilters()
            .Include(s => s.CourseGroup)
            .FirstOrDefaultAsync(s => s.Id == id && s.OrganizationId == organizationId, cancellationToken);

        if (session == null)
        {
            _logger.LogWarning("Validation failed: Session '{SessionId}' was not found for status update.", id);
            throw new NotFoundException("Session", id);
        }

        // 2. Instructor authorization check
        await ValidateInstructorAuthorizationForSessionAsync(session, currentUserId, organizationId, null, cancellationToken);

        // 3. Cannot change status of a cancelled session
        if (session.Status == SessionConstants.Statuses.Cancelled && status != SessionConstants.Statuses.Cancelled)
        {
            _logger.LogWarning("Validation failed: Attempted to change status of cancelled session '{SessionId}'.", id);
            throw new ConflictException("Cannot change the status of a cancelled session.");
        }

        // 4. Cannot revert a completed session back to draft or scheduled
        if (session.Status == SessionConstants.Statuses.Completed &&
            (status == SessionConstants.Statuses.Draft || status == SessionConstants.Statuses.Scheduled))
        {
            _logger.LogWarning("Validation failed: Attempted to revert completed session '{SessionId}' back to draft or scheduled.", id);
            throw new ConflictException("Cannot transition a completed session back to draft or scheduled.");
        }

        // 5. Cannot mark future session as completed
        if (status == SessionConstants.Statuses.Completed && session.StartTime > DateTime.UtcNow)
        {
            _logger.LogWarning("Validation failed: Attempted to mark future session '{SessionId}' as completed.", id);
            throw new ConflictException("Cannot mark a future session as completed.");
        }
    }

    public async Task ValidateDeleteAsync(Guid id, Guid organizationId, Guid? currentUserId = null, CancellationToken cancellationToken = default)
    {
        // 1. Verify Session exists within organization
        var session = await _dbContext.Sessions
            .IgnoreQueryFilters()
            .Include(s => s.CourseGroup)
            .FirstOrDefaultAsync(s => s.Id == id && s.OrganizationId == organizationId, cancellationToken);

        if (session == null)
        {
            _logger.LogWarning("Validation failed: Session '{SessionId}' was not found for deletion.", id);
            throw new NotFoundException("Session", id);
        }

        // 2. Instructor authorization check
        await ValidateInstructorAuthorizationForSessionAsync(session, currentUserId, organizationId, null, cancellationToken);

        // 3. Verify no active attendance records
        var attendanceCount = await _dbContext.AttendanceRecords
            .IgnoreQueryFilters()
            .CountAsync(a => a.SessionId == id, cancellationToken);

        if (attendanceCount > 0)
        {
            _logger.LogWarning("Validation failed: Cannot delete session '{SessionId}' because it has {AttendanceCount} attendance records.",
                id, attendanceCount);
            throw new ConflictException(
                $"Cannot delete session because it has associated attendance records ({attendanceCount} record(s)). " +
                "Consider cancelling the session instead.");
        }
    }

    #region Helper Methods

    private static void ValidateCourseGroupIsActive(CourseGroup courseGroup)
    {
        if (courseGroup.Status != CourseGroupConstants.Statuses.Active)
        {
            throw new ConflictException(
                $"Cannot schedule a session for a course group that is not active. Current group status is '{CourseGroupConstants.Statuses.GetName(courseGroup.Status)}'.");
        }

        if (courseGroup.Course != null && courseGroup.Course.Status == CourseConstants.Statuses.Archived)
        {
            throw new ConflictException("Cannot schedule a session for a course group whose course is archived.");
        }
    }

    private static void ValidateCourseGroupDateBoundary(DateTime startTime, DateTime endTime, CourseGroup courseGroup)
    {
        var sessionStartDate = DateOnly.FromDateTime(startTime);
        var sessionEndDate = DateOnly.FromDateTime(endTime);

        if (sessionStartDate < courseGroup.StartDate || sessionEndDate > courseGroup.EndDate)
        {
            throw new ValidationException("StartTime",
                $"Session must be scheduled within the course group date range ({courseGroup.StartDate:yyyy-MM-dd} to {courseGroup.EndDate:yyyy-MM-dd}).");
        }
    }

    private static void ValidateSessionNotEndedOrCancelled(Session session, string operation)
    {
        if (session.Status == SessionConstants.Statuses.Cancelled)
        {
            throw new ConflictException($"Cannot {operation} a cancelled session.");
        }

        if (session.Status == SessionConstants.Statuses.Completed)
        {
            throw new ConflictException($"Cannot {operation} a completed session.");
        }
    }

    private async Task ValidateInstructorAuthorizationForCourseGroupAsync(
        CourseGroup courseGroup,
        Guid? currentUserId,
        Guid organizationId,
        Guid? targetInstructorId,
        CancellationToken cancellationToken)
    {
        if (!currentUserId.HasValue || currentUserId.Value == Guid.Empty)
            return;

        var (isOrgStaff, instructor) = await ResolveCallerPermissionsAsync(currentUserId.Value, organizationId, cancellationToken);
        if (isOrgStaff)
            return;

        if (instructor == null)
            throw new ForbiddenAccessException("No active instructor profile is associated with this user.");

        var isGroupInstructor = courseGroup.InstructorId == instructor.Id;

        if (!isGroupInstructor)
        {
            _logger.LogWarning("Instructor '{InstructorId}' attempted to schedule a session for course group '{CourseGroupId}' which they do not teach.",
                instructor.Id, courseGroup.Id);
            throw new ForbiddenAccessException("You are not authorized to schedule sessions for this course group.");
        }

        if (targetInstructorId.HasValue && targetInstructorId.Value != instructor.Id)
        {
            _logger.LogWarning("Instructor '{InstructorId}' attempted to assign a session to another instructor '{TargetInstructorId}'.",
                instructor.Id, targetInstructorId.Value);
            throw new ForbiddenAccessException("Instructors cannot assign sessions to other instructors.");
        }
    }

    private async Task ValidateInstructorAuthorizationForSessionAsync(
        Session session,
        Guid? currentUserId,
        Guid organizationId,
        Guid? targetInstructorId,
        CancellationToken cancellationToken)
    {
        if (!currentUserId.HasValue || currentUserId.Value == Guid.Empty)
            return;

        var (isOrgStaff, instructor) = await ResolveCallerPermissionsAsync(currentUserId.Value, organizationId, cancellationToken);
        if (isOrgStaff)
            return;

        if (instructor == null)
            throw new ForbiddenAccessException("No active instructor profile is associated with this user.");

        var isSessionInstructor = session.InstructorId == instructor.Id;
        var isGroupInstructor = session.CourseGroup != null && session.CourseGroup.InstructorId == instructor.Id;

        if (!isSessionInstructor && !isGroupInstructor)
        {
            _logger.LogWarning("Instructor '{InstructorId}' attempted to manage session '{SessionId}' which they do not teach.",
                instructor.Id, session.Id);
            throw new ForbiddenAccessException("You are not authorized to manage this session as you are not the instructor for this session or its course group.");
        }

        if (targetInstructorId.HasValue && targetInstructorId.Value != instructor.Id)
        {
            _logger.LogWarning("Instructor '{InstructorId}' attempted to reassign session '{SessionId}' to another instructor '{TargetInstructorId}'.",
                instructor.Id, session.Id, targetInstructorId.Value);
            throw new ForbiddenAccessException("Instructors cannot reassign sessions to other instructors.");
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
