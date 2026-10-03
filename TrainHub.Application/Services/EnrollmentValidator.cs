using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TrainHub.Application.Common.Exceptions;
using TrainHub.Application.Features.Enrollments.DTOs;
using TrainHub.Application.Interfaces;
using TrainHub.Domain.Constants;

namespace TrainHub.Application.Services;

/// <summary>
/// Validates business rules, constraints, and relational integrity for Enrollments.
/// </summary>
public class EnrollmentValidator : IEnrollmentValidator
{
    private readonly ITrainHubDbContext _dbContext;
    private readonly ILogger<EnrollmentValidator> _logger;

    public EnrollmentValidator(ITrainHubDbContext dbContext, ILogger<EnrollmentValidator> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public void ValidateQuery(EnrollmentQueryParameters query)
    {
        ArgumentNullException.ThrowIfNull(query);

        var errors = new Dictionary<string, string[]>();

        if (query.PageNumber < 1)
        {
            errors[nameof(query.PageNumber)] = ["Page number must be at least 1."];
        }

        if (query.PageSize < 1 || query.PageSize > EnrollmentConstants.MaxPageSize)
        {
            errors[nameof(query.PageSize)] = [$"Page size must be between 1 and {EnrollmentConstants.MaxPageSize}."];
        }

        if (query.Status.HasValue && !EnrollmentConstants.IsValidStatus(query.Status.Value))
        {
            errors[nameof(query.Status)] = [$"Invalid status '{query.Status.Value}'. Allowed values: {string.Join(", ", EnrollmentConstants.Statuses.All)}."];
        }

        if (!string.IsNullOrWhiteSpace(query.SortBy) && !EnrollmentConstants.IsValidSortField(query.SortBy))
        {
            errors[nameof(query.SortBy)] = [$"Invalid sort field '{query.SortBy}'. Allowed values: {string.Join(", ", EnrollmentConstants.AllowedSortFields)}."];
        }

        if (errors.Count > 0)
        {
            _logger.LogWarning("Validation failed for enrollment query parameters: {ErrorCount} errors.", errors.Count);
            throw new ValidationException(errors);
        }
    }

    public async Task ValidateCreateAsync(CreateEnrollmentDto dto, Guid organizationId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        if (dto.PaidAmount.HasValue && !EnrollmentConstants.IsValidPaidAmount(dto.PaidAmount.Value))
        {
            _logger.LogWarning("Validation failed: Invalid paid amount '{PaidAmount}'.", dto.PaidAmount.Value);
            throw new ValidationException(nameof(dto.PaidAmount),
                $"PaidAmount must be between {EnrollmentConstants.MinPaidAmount} and {EnrollmentConstants.MaxPaidAmount}.");
        }

        if (!EnrollmentConstants.IsValidStatus(dto.Status))
        {
            _logger.LogWarning("Validation failed: Invalid status '{Status}'.", dto.Status);
            throw new ValidationException(nameof(dto.Status),
                $"Invalid status '{dto.Status}'. Allowed values: {string.Join(", ", EnrollmentConstants.Statuses.All)}.");
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

        // 3. Verify CourseGroup is Active
        if (courseGroup.Status != CourseGroupConstants.Statuses.Active)
        {
            _logger.LogWarning("Validation failed: Cannot enroll in non-active course group '{CourseGroupId}' with status '{Status}'.",
                dto.CourseGroupId, courseGroup.Status);
            throw new ConflictException(
                $"Cannot enroll in a course group that is not active. Current group status is '{CourseGroupConstants.Statuses.GetName(courseGroup.Status)}'.");
        }

        // 4. Verify CourseGroup capacity
        var activeEnrolledCount = await _dbContext.Enrollments
            .IgnoreQueryFilters()
            .CountAsync(e => e.CourseGroupId == dto.CourseGroupId && e.Status != EnrollmentConstants.Statuses.Cancelled, cancellationToken);

        if (activeEnrolledCount >= courseGroup.Capacity)
        {
            _logger.LogWarning("Validation failed: Course group '{CourseGroupId}' is at maximum capacity ({Capacity}).",
                dto.CourseGroupId, courseGroup.Capacity);
            throw new ConflictException($"Course group '{courseGroup.Name}' has reached its maximum capacity of {courseGroup.Capacity}.");
        }

        // 5. Verify Student exists within the organization
        var student = await _dbContext.Students
            .IgnoreQueryFilters()
            .Include(s => s.User)
            .FirstOrDefaultAsync(s => s.Id == dto.StudentId && s.OrganizationId == organizationId, cancellationToken);

        if (student == null)
        {
            _logger.LogWarning("Validation failed: Student '{StudentId}' does not exist in organization '{OrganizationId}'.",
                dto.StudentId, organizationId);
            throw new NotFoundException("Student", dto.StudentId);
        }

        // 6. Verify Student user account is active
        if (student.User != null && !student.User.IsActive)
        {
            _logger.LogWarning("Validation failed: Student '{StudentId}' has an inactive user account.", dto.StudentId);
            throw new ConflictException("Cannot enroll a student whose user account is inactive.");
        }

        // 7. Verify Student is not already enrolled in this CourseGroup
        var alreadyEnrolled = await _dbContext.Enrollments
            .IgnoreQueryFilters()
            .AnyAsync(e => e.CourseGroupId == dto.CourseGroupId && e.StudentId == dto.StudentId, cancellationToken);

        if (alreadyEnrolled)
        {
            _logger.LogWarning("Validation failed: Student '{StudentId}' is already enrolled in course group '{CourseGroupId}'.",
                dto.StudentId, dto.CourseGroupId);
            throw new ConflictException("Student is already enrolled in this course group.");
        }
    }

    public async Task ValidateUpdateAsync(Guid id, UpdateEnrollmentDto dto, Guid organizationId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        if (dto.PaidAmount.HasValue && !EnrollmentConstants.IsValidPaidAmount(dto.PaidAmount.Value))
        {
            _logger.LogWarning("Validation failed: Invalid paid amount '{PaidAmount}'.", dto.PaidAmount.Value);
            throw new ValidationException(nameof(dto.PaidAmount),
                $"PaidAmount must be between {EnrollmentConstants.MinPaidAmount} and {EnrollmentConstants.MaxPaidAmount}.");
        }

        if (dto.Status.HasValue && !EnrollmentConstants.IsValidStatus(dto.Status.Value))
        {
            _logger.LogWarning("Validation failed: Invalid status '{Status}'.", dto.Status.Value);
            throw new ValidationException(nameof(dto.Status),
                $"Invalid status '{dto.Status.Value}'. Allowed values: {string.Join(", ", EnrollmentConstants.Statuses.All)}.");
        }

        // 1. Verify Enrollment exists
        var enrollment = await _dbContext.Enrollments
            .IgnoreQueryFilters()
            .Include(e => e.CourseGroup)
            .FirstOrDefaultAsync(e => e.Id == id && e.OrganizationId == organizationId, cancellationToken);

        if (enrollment == null)
        {
            _logger.LogWarning("Validation failed: Enrollment '{EnrollmentId}' was not found for update.", id);
            throw new NotFoundException("Enrollment", id);
        }

        // 2. Cannot modify a cancelled enrollment unless activating
        if (enrollment.Status == EnrollmentConstants.Statuses.Cancelled)
        {
            if (!dto.Status.HasValue || dto.Status.Value != EnrollmentConstants.Statuses.Active)
            {
                _logger.LogWarning("Validation failed: Attempted to modify cancelled enrollment '{EnrollmentId}'.", id);
                throw new ConflictException("Cannot modify a cancelled enrollment.");
            }

            // Check capacity when reactivating
            var activeEnrolledCount = await _dbContext.Enrollments
                .IgnoreQueryFilters()
                .CountAsync(e => e.CourseGroupId == enrollment.CourseGroupId && e.Status != EnrollmentConstants.Statuses.Cancelled, cancellationToken);

            if (activeEnrolledCount >= enrollment.CourseGroup.Capacity)
            {
                _logger.LogWarning("Validation failed: Cannot reactivate enrollment '{EnrollmentId}' because course group is at capacity.", id);
                throw new ConflictException("Cannot reactivate enrollment because the course group has reached its maximum capacity.");
            }
        }

        // 3. Cannot revert a completed enrollment back to pending
        if (enrollment.Status == EnrollmentConstants.Statuses.Completed && dto.Status == EnrollmentConstants.Statuses.Pending)
        {
            _logger.LogWarning("Validation failed: Attempted to transition completed enrollment '{EnrollmentId}' back to pending.", id);
            throw new ConflictException("Cannot transition a completed enrollment back to pending.");
        }
    }

    public async Task ValidateUpdateStatusAsync(Guid id, byte status, CancellationToken cancellationToken = default)
    {
        if (!EnrollmentConstants.IsValidStatus(status))
        {
            _logger.LogWarning("Validation failed: Invalid enrollment status '{Status}'.", status);
            throw new ValidationException(nameof(status),
                $"Invalid enrollment status '{status}'. Allowed values: {string.Join(", ", EnrollmentConstants.Statuses.All)}.");
        }

        // 1. Verify Enrollment exists
        var enrollment = await _dbContext.Enrollments
            .IgnoreQueryFilters()
            .Include(e => e.CourseGroup)
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken);

        if (enrollment == null)
        {
            _logger.LogWarning("Validation failed: Enrollment '{EnrollmentId}' was not found for status update.", id);
            throw new NotFoundException("Enrollment", id);
        }

        // 2. Prevent changing status of cancelled enrollment unless activating
        if (enrollment.Status == EnrollmentConstants.Statuses.Cancelled && status != EnrollmentConstants.Statuses.Active)
        {
            _logger.LogWarning("Validation failed: Attempted to change status of cancelled enrollment '{EnrollmentId}'.", id);
            throw new ConflictException("Cannot change status of a cancelled enrollment.");
        }

        // 3. Cannot revert a completed enrollment back to pending
        if (enrollment.Status == EnrollmentConstants.Statuses.Completed && status == EnrollmentConstants.Statuses.Pending)
        {
            _logger.LogWarning("Validation failed: Attempted to transition completed enrollment '{EnrollmentId}' back to pending.", id);
            throw new ConflictException("Cannot transition a completed enrollment back to pending.");
        }

        // 4. If reactivating from Cancelled, check capacity
        if (status == EnrollmentConstants.Statuses.Active && enrollment.Status == EnrollmentConstants.Statuses.Cancelled)
        {
            var activeEnrolledCount = await _dbContext.Enrollments
                .IgnoreQueryFilters()
                .CountAsync(e => e.CourseGroupId == enrollment.CourseGroupId && e.Status != EnrollmentConstants.Statuses.Cancelled, cancellationToken);

            if (activeEnrolledCount >= enrollment.CourseGroup.Capacity)
            {
                _logger.LogWarning("Validation failed: Cannot activate enrollment '{EnrollmentId}' because course group is full.", id);
                throw new ConflictException("Cannot activate enrollment because the course group has reached its maximum capacity.");
            }
        }
    }

    public async Task ValidateDeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        // 1. Verify Enrollment exists
        var enrollment = await _dbContext.Enrollments
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken);

        if (enrollment == null)
        {
            _logger.LogWarning("Validation failed: Enrollment '{EnrollmentId}' was not found for deletion.", id);
            throw new NotFoundException("Enrollment", id);
        }

        // 2. Verify no active attendance records for this student in sessions of this group
        var hasAttendance = await _dbContext.AttendanceRecords
            .IgnoreQueryFilters()
            .Include(a => a.Session)
            .AnyAsync(a => a.StudentId == enrollment.StudentId && a.Session.CourseGroupId == enrollment.CourseGroupId, cancellationToken);

        if (hasAttendance)
        {
            _logger.LogWarning("Validation failed: Cannot delete enrollment '{EnrollmentId}' because student has associated attendance records.", id);
            throw new ConflictException(
                "Cannot delete enrollment because the student has attendance records in this course group. Consider cancelling the enrollment instead.");
        }
    }
}
