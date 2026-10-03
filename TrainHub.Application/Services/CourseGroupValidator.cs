using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TrainHub.Application.Common.Exceptions;
using TrainHub.Application.Features.CourseGroups.DTOs;
using TrainHub.Application.Interfaces;
using TrainHub.Domain.Constants;

namespace TrainHub.Application.Services;

/// <summary>
/// Validates business rules, unique constraints, and relational integrity for CourseGroups.
/// </summary>
public class CourseGroupValidator : ICourseGroupValidator
{
    private readonly ITrainHubDbContext _dbContext;
    private readonly ILogger<CourseGroupValidator> _logger;

    public CourseGroupValidator(ITrainHubDbContext dbContext, ILogger<CourseGroupValidator> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public void ValidateQuery(CourseGroupQueryParameters query)
    {
        ArgumentNullException.ThrowIfNull(query);

        var errors = new Dictionary<string, string[]>();

        if (query.PageNumber < 1)
        {
            errors[nameof(query.PageNumber)] = ["Page number must be at least 1."];
        }

        if (query.PageSize < 1 || query.PageSize > CourseGroupConstants.MaxPageSize)
        {
            errors[nameof(query.PageSize)] = [$"Page size must be between 1 and {CourseGroupConstants.MaxPageSize}."];
        }

        if (!string.IsNullOrWhiteSpace(query.SearchTerm) && query.SearchTerm.Trim().Length > CourseGroupConstants.SearchTermMaxLength)
        {
            errors[nameof(query.SearchTerm)] = [$"Search term cannot exceed {CourseGroupConstants.SearchTermMaxLength} characters."];
        }

        if (query.Status.HasValue && !CourseGroupConstants.IsValidStatus(query.Status.Value))
        {
            errors[nameof(query.Status)] = [$"Invalid status '{query.Status.Value}'. Allowed values: {string.Join(", ", CourseGroupConstants.Statuses.All)}."];
        }

        if (!string.IsNullOrWhiteSpace(query.SortBy) && !CourseGroupConstants.IsValidSortField(query.SortBy))
        {
            errors[nameof(query.SortBy)] = [$"Invalid sort field '{query.SortBy}'. Allowed values: {string.Join(", ", CourseGroupConstants.AllowedSortFields)}."];
        }

        if (errors.Count > 0)
        {
            _logger.LogWarning("Validation failed for course group query parameters: {ErrorCount} errors.", errors.Count);
            throw new ValidationException(errors);
        }
    }

    public async Task ValidateCreateAsync(CreateCourseGroupDto dto, Guid organizationId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        if (!CourseGroupConstants.IsValidDateRange(dto.StartDate, dto.EndDate))
        {
            _logger.LogWarning("Validation failed: EndDate '{EndDate}' is earlier than StartDate '{StartDate}'.", dto.EndDate, dto.StartDate);
            throw new ValidationException(nameof(dto.EndDate), "EndDate cannot be earlier than StartDate.");
        }

        if (dto.Capacity.HasValue && !CourseGroupConstants.IsValidCapacity(dto.Capacity.Value))
        {
            _logger.LogWarning("Validation failed: Invalid capacity '{Capacity}'.", dto.Capacity.Value);
            throw new ValidationException(nameof(dto.Capacity),
                $"Capacity must be between {CourseGroupConstants.MinCapacity} and {CourseGroupConstants.MaxCapacity}.");
        }

        if (!CourseGroupConstants.IsValidStatus(dto.Status))
        {
            _logger.LogWarning("Validation failed: Invalid status '{Status}'.", dto.Status);
            throw new ValidationException(nameof(dto.Status),
                $"Invalid status '{dto.Status}'. Allowed values: {string.Join(", ", CourseGroupConstants.Statuses.All)}.");
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

        // 2. Verify Course exists within the organization (tenant boundary) and is not archived
        var course = await _dbContext.Courses
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.Id == dto.CourseId && c.OrganizationId == organizationId, cancellationToken);

        if (course == null)
        {
            _logger.LogWarning("Validation failed: Course '{CourseId}' does not exist in organization '{OrganizationId}'.",
                dto.CourseId, organizationId);
            throw new NotFoundException("Course", dto.CourseId);
        }

        if (course.Status == CourseConstants.Statuses.Archived)
        {
            _logger.LogWarning("Validation failed: Cannot create course group for archived course '{CourseId}'.", dto.CourseId);
            throw new ConflictException("Cannot create a course group for an archived course.");
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

        // 4. Verify CourseGroup name uniqueness within the same Course in the Organization
        var trimmedName = dto.Name.Trim();
        var nameExists = await _dbContext.CourseGroups
            .IgnoreQueryFilters()
            .AnyAsync(cg => cg.OrganizationId == organizationId && cg.CourseId == dto.CourseId && cg.Name == trimmedName, cancellationToken);

        if (nameExists)
        {
            _logger.LogWarning("Validation failed: Course group named '{GroupName}' already exists for course '{CourseId}' in organization '{OrganizationId}'.",
                trimmedName, dto.CourseId, organizationId);
            throw new ConflictException($"A course group named '{trimmedName}' already exists for this course.");
        }
    }

    public async Task ValidateUpdateAsync(Guid id, UpdateCourseGroupDto dto, Guid organizationId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        if (!CourseGroupConstants.IsValidDateRange(dto.StartDate, dto.EndDate))
        {
            _logger.LogWarning("Validation failed: EndDate '{EndDate}' is earlier than StartDate '{StartDate}'.", dto.EndDate, dto.StartDate);
            throw new ValidationException(nameof(dto.EndDate), "EndDate cannot be earlier than StartDate.");
        }

        if (dto.Capacity.HasValue && !CourseGroupConstants.IsValidCapacity(dto.Capacity.Value))
        {
            _logger.LogWarning("Validation failed: Invalid capacity '{Capacity}'.", dto.Capacity.Value);
            throw new ValidationException(nameof(dto.Capacity),
                $"Capacity must be between {CourseGroupConstants.MinCapacity} and {CourseGroupConstants.MaxCapacity}.");
        }

        if (dto.Status.HasValue && !CourseGroupConstants.IsValidStatus(dto.Status.Value))
        {
            _logger.LogWarning("Validation failed: Invalid status '{Status}'.", dto.Status.Value);
            throw new ValidationException(nameof(dto.Status),
                $"Invalid status '{dto.Status.Value}'. Allowed values: {string.Join(", ", CourseGroupConstants.Statuses.All)}.");
        }

        // 1. Verify CourseGroup exists
        var group = await _dbContext.CourseGroups
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(cg => cg.Id == id, cancellationToken);

        if (group == null)
        {
            _logger.LogWarning("Validation failed: Course group '{CourseGroupId}' was not found for update.", id);
            throw new NotFoundException("CourseGroup", id);
        }

        // 2. Prevent updating a cancelled group
        if (group.Status == CourseGroupConstants.Statuses.Cancelled)
        {
            _logger.LogWarning("Validation failed: Attempted to update cancelled course group '{CourseGroupId}'.", id);
            throw new ConflictException("Cannot modify a cancelled course group.");
        }

        // 3. Verify Capacity is not reduced below current enrolled students count
        if (dto.Capacity.HasValue)
        {
            var enrolledCount = await _dbContext.Enrollments
                .IgnoreQueryFilters()
                .CountAsync(e => e.CourseGroupId == id, cancellationToken);

            if (dto.Capacity.Value < enrolledCount)
            {
                _logger.LogWarning("Validation failed: Cannot reduce capacity to {NewCapacity} when {EnrolledCount} students are enrolled.",
                    dto.Capacity.Value, enrolledCount);
                throw new ConflictException($"Capacity cannot be reduced to {dto.Capacity.Value} because {enrolledCount} student(s) are already enrolled.");
            }
        }

        // 4. Verify Instructor exists within the organization and is active if provided
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

        // 5. Verify CourseGroup name uniqueness within the same Course (excluding self)
        var trimmedName = dto.Name.Trim();
        var nameExists = await _dbContext.CourseGroups
            .IgnoreQueryFilters()
            .AnyAsync(cg => cg.OrganizationId == organizationId && cg.CourseId == group.CourseId && cg.Name == trimmedName && cg.Id != id, cancellationToken);

        if (nameExists)
        {
            _logger.LogWarning("Validation failed: Course group name '{GroupName}' is already used by another group for course '{CourseId}'.",
                trimmedName, group.CourseId);
            throw new ConflictException($"A course group named '{trimmedName}' already exists for this course.");
        }
    }

    public async Task ValidateAssignInstructorAsync(Guid id, Guid? instructorId, Guid organizationId, CancellationToken cancellationToken = default)
    {
        // 1. Verify CourseGroup exists
        var group = await _dbContext.CourseGroups
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(cg => cg.Id == id, cancellationToken);

        if (group == null)
        {
            _logger.LogWarning("Validation failed: Course group '{CourseGroupId}' was not found for instructor assignment.", id);
            throw new NotFoundException("CourseGroup", id);
        }

        // 2. Validate group status
        if (group.Status == CourseGroupConstants.Statuses.Cancelled)
        {
            _logger.LogWarning("Validation failed: Attempted to assign instructor to cancelled course group '{CourseGroupId}'.", id);
            throw new ConflictException("Cannot assign an instructor to a cancelled course group.");
        }

        if (group.Status == CourseGroupConstants.Statuses.Completed)
        {
            _logger.LogWarning("Validation failed: Attempted to assign instructor to completed course group '{CourseGroupId}'.", id);
            throw new ConflictException("Cannot assign an instructor to a completed course group.");
        }

        // 3. If assigning an instructor, verify they exist in the organization and are active
        if (instructorId.HasValue)
        {
            var instructor = await _dbContext.Instructors
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(i => i.Id == instructorId.Value && i.OrganizationId == organizationId, cancellationToken);

            if (instructor == null)
            {
                _logger.LogWarning("Validation failed: Instructor '{InstructorId}' does not exist in organization '{OrganizationId}'.",
                    instructorId.Value, organizationId);
                throw new NotFoundException("Instructor", instructorId.Value);
            }

            var user = await _dbContext.ApplicationUsers
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(u => u.Id == instructor.UserId, cancellationToken);

            if (user != null && !user.IsActive)
            {
                _logger.LogWarning("Validation failed: Instructor '{InstructorId}' has an inactive user account.", instructorId.Value);
                throw new ConflictException("Cannot assign an instructor whose user account is inactive.");
            }
        }
    }

    public async Task ValidateUpdateCapacityAsync(Guid id, int capacity, CancellationToken cancellationToken = default)
    {
        if (!CourseGroupConstants.IsValidCapacity(capacity))
        {
            _logger.LogWarning("Validation failed: Invalid capacity '{Capacity}'.", capacity);
            throw new ValidationException(nameof(capacity),
                $"Capacity must be between {CourseGroupConstants.MinCapacity} and {CourseGroupConstants.MaxCapacity}.");
        }

        // 1. Verify CourseGroup exists
        var group = await _dbContext.CourseGroups
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(cg => cg.Id == id, cancellationToken);

        if (group == null)
        {
            _logger.LogWarning("Validation failed: Course group '{CourseGroupId}' was not found for capacity update.", id);
            throw new NotFoundException("CourseGroup", id);
        }

        // 2. Validate group status
        if (group.Status == CourseGroupConstants.Statuses.Cancelled)
        {
            _logger.LogWarning("Validation failed: Attempted to update capacity of cancelled course group '{CourseGroupId}'.", id);
            throw new ConflictException("Cannot update capacity of a cancelled course group.");
        }

        if (group.Status == CourseGroupConstants.Statuses.Completed)
        {
            _logger.LogWarning("Validation failed: Attempted to update capacity of completed course group '{CourseGroupId}'.", id);
            throw new ConflictException("Cannot update capacity of a completed course group.");
        }

        // 3. Verify capacity is not less than current enrolled students count
        var enrolledCount = await _dbContext.Enrollments
            .IgnoreQueryFilters()
            .CountAsync(e => e.CourseGroupId == id, cancellationToken);

        if (capacity < enrolledCount)
        {
            _logger.LogWarning("Validation failed: Cannot reduce capacity to {NewCapacity} when {EnrolledCount} students are enrolled.",
                capacity, enrolledCount);
            throw new ConflictException($"Capacity cannot be reduced to {capacity} because {enrolledCount} student(s) are already enrolled.");
        }
    }

    public async Task ValidateUpdateStatusAsync(Guid id, byte status, CancellationToken cancellationToken = default)
    {
        if (!CourseGroupConstants.IsValidStatus(status))
        {
            _logger.LogWarning("Validation failed: Invalid course group status '{Status}'.", status);
            throw new ValidationException(nameof(status),
                $"Invalid course group status '{status}'. Allowed values are: {string.Join(", ", CourseGroupConstants.Statuses.All)}.");
        }

        // 1. Verify CourseGroup exists
        var group = await _dbContext.CourseGroups
            .IgnoreQueryFilters()
            .Include(cg => cg.Course)
            .FirstOrDefaultAsync(cg => cg.Id == id, cancellationToken);

        if (group == null)
        {
            _logger.LogWarning("Validation failed: Course group '{CourseGroupId}' was not found for status update.", id);
            throw new NotFoundException("CourseGroup", id);
        }

        // 2. Cannot modify a cancelled course group
        if (group.Status == CourseGroupConstants.Statuses.Cancelled && status != CourseGroupConstants.Statuses.Cancelled)
        {
            _logger.LogWarning("Validation failed: Attempted to change status of cancelled course group '{CourseGroupId}'.", id);
            throw new ConflictException("Cannot change the status of a cancelled course group.");
        }

        // 3. Cannot revert a completed group back to draft
        if (group.Status == CourseGroupConstants.Statuses.Completed && status == CourseGroupConstants.Statuses.Draft)
        {
            _logger.LogWarning("Validation failed: Attempted to revert completed course group '{CourseGroupId}' back to draft.", id);
            throw new ConflictException("Cannot transition a completed course group back to draft.");
        }

        // 4. If activating, verify parent Course is not archived
        if (status == CourseGroupConstants.Statuses.Active && group.Course.Status == CourseConstants.Statuses.Archived)
        {
            _logger.LogWarning("Validation failed: Cannot activate course group '{CourseGroupId}' because parent course '{CourseId}' is archived.",
                id, group.CourseId);
            throw new ConflictException("Cannot activate a course group belonging to an archived course.");
        }
    }

    public async Task ValidateDeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        // 1. Verify CourseGroup exists
        var group = await _dbContext.CourseGroups
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(cg => cg.Id == id, cancellationToken);

        if (group == null)
        {
            _logger.LogWarning("Validation failed: Course group '{CourseGroupId}' was not found for deletion.", id);
            throw new NotFoundException("CourseGroup", id);
        }

        // 2. Verify no active dependent records (Enrollments, Sessions)
        var enrollmentCount = await _dbContext.Enrollments
            .IgnoreQueryFilters()
            .CountAsync(e => e.CourseGroupId == id, cancellationToken);

        var sessionCount = await _dbContext.Sessions
            .IgnoreQueryFilters()
            .CountAsync(s => s.CourseGroupId == id, cancellationToken);

        if (enrollmentCount > 0 || sessionCount > 0)
        {
            _logger.LogWarning("Validation failed: Cannot delete course group '{CourseGroupId}' due to active dependencies ({EnrollmentCount} enrollments, {SessionCount} sessions).",
                id, enrollmentCount, sessionCount);
            throw new ConflictException(
                $"Cannot delete course group '{group.Name}' because it has associated records " +
                $"({enrollmentCount} enrollment(s), {sessionCount} session(s)). " +
                "Please remove these records first.");
        }
    }
}
