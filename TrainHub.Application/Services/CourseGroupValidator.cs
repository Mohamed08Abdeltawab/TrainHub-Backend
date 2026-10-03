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

        // 1. Verify Organization exists
        var orgExists = await _dbContext.Organizations
            .IgnoreQueryFilters()
            .AnyAsync(o => o.Id == organizationId, cancellationToken);

        if (!orgExists)
        {
            _logger.LogWarning("Validation failed: Organization '{OrganizationId}' does not exist.", organizationId);
            throw new NotFoundException("Organization", organizationId);
        }

        // 2. Verify Course exists within the organization (tenant boundary)
        var courseExists = await _dbContext.Courses
            .IgnoreQueryFilters()
            .AnyAsync(c => c.Id == dto.CourseId && c.OrganizationId == organizationId, cancellationToken);

        if (!courseExists)
        {
            _logger.LogWarning("Validation failed: Course '{CourseId}' does not exist in organization '{OrganizationId}'.",
                dto.CourseId, organizationId);
            throw new NotFoundException("Course", dto.CourseId);
        }

        // 3. Verify Instructor exists within the organization if provided
        if (dto.InstructorId.HasValue)
        {
            var instructorExists = await _dbContext.Instructors
                .IgnoreQueryFilters()
                .AnyAsync(i => i.Id == dto.InstructorId.Value && i.OrganizationId == organizationId, cancellationToken);

            if (!instructorExists)
            {
                _logger.LogWarning("Validation failed: Instructor '{InstructorId}' does not exist in organization '{OrganizationId}'.",
                    dto.InstructorId.Value, organizationId);
                throw new NotFoundException("Instructor", dto.InstructorId.Value);
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

        // 1. Verify CourseGroup exists
        var group = await _dbContext.CourseGroups
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(cg => cg.Id == id, cancellationToken);

        if (group == null)
        {
            _logger.LogWarning("Validation failed: Course group '{CourseGroupId}' was not found for update.", id);
            throw new NotFoundException("CourseGroup", id);
        }

        // 2. Verify Instructor exists within the organization if provided
        if (dto.InstructorId.HasValue)
        {
            var instructorExists = await _dbContext.Instructors
                .IgnoreQueryFilters()
                .AnyAsync(i => i.Id == dto.InstructorId.Value && i.OrganizationId == organizationId, cancellationToken);

            if (!instructorExists)
            {
                _logger.LogWarning("Validation failed: Instructor '{InstructorId}' does not exist in organization '{OrganizationId}'.",
                    dto.InstructorId.Value, organizationId);
                throw new NotFoundException("Instructor", dto.InstructorId.Value);
            }
        }

        // 3. Verify CourseGroup name uniqueness within the same Course (excluding self)
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
