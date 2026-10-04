using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TrainHub.Application.Common.Exceptions;
using TrainHub.Application.Features.Sessions.DTOs;
using TrainHub.Application.Interfaces;
using TrainHub.Domain.Constants;

namespace TrainHub.Application.Services;

/// <summary>
/// Validates business rules, constraints, and relational integrity for Sessions.
/// </summary>
public class SessionValidator : ISessionValidator
{
    private readonly ITrainHubDbContext _dbContext;
    private readonly ILogger<SessionValidator> _logger;

    public SessionValidator(ITrainHubDbContext dbContext, ILogger<SessionValidator> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
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

    public async Task ValidateCreateAsync(CreateSessionDto dto, Guid organizationId, CancellationToken cancellationToken = default)
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

        // 2. Verify CourseGroup exists within the organization
        var courseGroup = await _dbContext.CourseGroups
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(cg => cg.Id == dto.CourseGroupId && cg.OrganizationId == organizationId, cancellationToken);

        if (courseGroup == null)
        {
            _logger.LogWarning("Validation failed: Course group '{CourseGroupId}' does not exist in organization '{OrganizationId}'.",
                dto.CourseGroupId, organizationId);
            throw new NotFoundException("CourseGroup", dto.CourseGroupId);
        }

        // 3. Verify CourseGroup is not Cancelled or Completed
        if (courseGroup.Status == CourseGroupConstants.Statuses.Cancelled)
        {
            _logger.LogWarning("Validation failed: Cannot schedule a session for cancelled course group '{CourseGroupId}'.", dto.CourseGroupId);
            throw new ConflictException("Cannot schedule a session for a cancelled course group.");
        }

        if (courseGroup.Status == CourseGroupConstants.Statuses.Completed)
        {
            _logger.LogWarning("Validation failed: Cannot schedule a session for completed course group '{CourseGroupId}'.", dto.CourseGroupId);
            throw new ConflictException("Cannot schedule a session for a completed course group.");
        }

        // 4. Verify Instructor exists within the organization and has active user account if provided
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
    }

    public async Task ValidateUpdateAsync(Guid id, UpdateSessionDto dto, Guid organizationId, CancellationToken cancellationToken = default)
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

        // 1. Verify Session exists within the organization
        var session = await _dbContext.Sessions
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.Id == id && s.OrganizationId == organizationId, cancellationToken);

        if (session == null)
        {
            _logger.LogWarning("Validation failed: Session '{SessionId}' was not found for update.", id);
            throw new NotFoundException("Session", id);
        }

        // 2. Cannot modify a cancelled session
        if (session.Status == SessionConstants.Statuses.Cancelled)
        {
            _logger.LogWarning("Validation failed: Attempted to modify cancelled session '{SessionId}'.", id);
            throw new ConflictException("Cannot modify a cancelled session.");
        }

        // 3. Verify Instructor exists within the organization and has active user account if provided
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
    }

    public async Task ValidateUpdateStatusAsync(Guid id, byte status, CancellationToken cancellationToken = default)
    {
        if (!SessionConstants.IsValidStatus(status))
        {
            _logger.LogWarning("Validation failed: Invalid status '{Status}'.", status);
            throw new ValidationException(nameof(status),
                $"Invalid status '{status}'. Allowed values: {string.Join(", ", SessionConstants.Statuses.All)}.");
        }

        // 1. Verify Session exists
        var session = await _dbContext.Sessions
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

        if (session == null)
        {
            _logger.LogWarning("Validation failed: Session '{SessionId}' was not found for status update.", id);
            throw new NotFoundException("Session", id);
        }

        // 2. Cannot change status of a cancelled session
        if (session.Status == SessionConstants.Statuses.Cancelled && status != SessionConstants.Statuses.Cancelled)
        {
            _logger.LogWarning("Validation failed: Attempted to change status of cancelled session '{SessionId}'.", id);
            throw new ConflictException("Cannot change the status of a cancelled session.");
        }

        // 3. Cannot revert a completed session back to draft or scheduled
        if (session.Status == SessionConstants.Statuses.Completed &&
            (status == SessionConstants.Statuses.Draft || status == SessionConstants.Statuses.Scheduled))
        {
            _logger.LogWarning("Validation failed: Attempted to revert completed session '{SessionId}' back to draft or scheduled.", id);
            throw new ConflictException("Cannot transition a completed session back to draft or scheduled.");
        }
    }

    public async Task ValidateDeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        // 1. Verify Session exists
        var session = await _dbContext.Sessions
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

        if (session == null)
        {
            _logger.LogWarning("Validation failed: Session '{SessionId}' was not found for deletion.", id);
            throw new NotFoundException("Session", id);
        }

        // 2. Verify no active attendance records
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
}
