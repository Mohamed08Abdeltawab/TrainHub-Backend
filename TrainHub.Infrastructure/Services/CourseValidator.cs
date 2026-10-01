using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TrainHub.Application.Common.Exceptions;
using TrainHub.Application.Features.Courses.DTOs;
using TrainHub.Application.Interfaces;
using TrainHub.Domain.Constants;
using TrainHub.Infrastructure.Data;

namespace TrainHub.Infrastructure.Services;

/// <summary>
/// Validates business rules, unique constraints, and relational integrity for Courses.
/// </summary>
public class CourseValidator : ICourseValidator
{
    private readonly TrainHubDbContext _dbContext;
    private readonly ILogger<CourseValidator> _logger;

    public CourseValidator(TrainHubDbContext dbContext, ILogger<CourseValidator> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public void ValidateQuery(CourseQueryParameters query)
    {
        ArgumentNullException.ThrowIfNull(query);

        var errors = new Dictionary<string, string[]>();

        if (query.PageNumber < 1)
        {
            errors[nameof(query.PageNumber)] = ["Page number must be at least 1."];
        }

        if (query.PageSize < 1 || query.PageSize > CourseConstants.MaxPageSize)
        {
            errors[nameof(query.PageSize)] = [$"Page size must be between 1 and {CourseConstants.MaxPageSize}."];
        }

        if (!string.IsNullOrWhiteSpace(query.SearchTerm) && query.SearchTerm.Trim().Length > CourseConstants.SearchTermMaxLength)
        {
            errors[nameof(query.SearchTerm)] = [$"Search term cannot exceed {CourseConstants.SearchTermMaxLength} characters."];
        }

        if (!string.IsNullOrWhiteSpace(query.Category) && query.Category.Trim().Length > CourseConstants.CategoryMaxLength)
        {
            errors[nameof(query.Category)] = [$"Category filter cannot exceed {CourseConstants.CategoryMaxLength} characters."];
        }

        if (query.MinPrice.HasValue && query.MinPrice.Value < CourseConstants.MinBasePrice)
        {
            errors[nameof(query.MinPrice)] = [$"MinPrice cannot be less than {CourseConstants.MinBasePrice}."];
        }

        if (query.MaxPrice.HasValue && query.MaxPrice.Value < CourseConstants.MinBasePrice)
        {
            errors[nameof(query.MaxPrice)] = [$"MaxPrice cannot be less than {CourseConstants.MinBasePrice}."];
        }

        if (query.MinPrice.HasValue && query.MaxPrice.HasValue && query.MinPrice.Value > query.MaxPrice.Value)
        {
            errors[nameof(query.MinPrice)] = ["MinPrice cannot be greater than MaxPrice."];
        }

        if (query.MinDuration.HasValue && query.MinDuration.Value < CourseConstants.MinDurationInHours)
        {
            errors[nameof(query.MinDuration)] = [$"MinDuration must be at least {CourseConstants.MinDurationInHours} hour(s)."];
        }

        if (query.MaxDuration.HasValue && query.MaxDuration.Value > CourseConstants.MaxDurationInHours)
        {
            errors[nameof(query.MaxDuration)] = [$"MaxDuration cannot exceed {CourseConstants.MaxDurationInHours} hours."];
        }

        if (query.MinDuration.HasValue && query.MaxDuration.HasValue && query.MinDuration.Value > query.MaxDuration.Value)
        {
            errors[nameof(query.MinDuration)] = ["MinDuration cannot be greater than MaxDuration."];
        }

        if (query.Status.HasValue && !CourseConstants.IsValidStatus(query.Status.Value))
        {
            errors[nameof(query.Status)] = [$"Invalid status '{query.Status.Value}'. Allowed values: {string.Join(", ", CourseConstants.Statuses.All)}."];
        }

        if (!string.IsNullOrWhiteSpace(query.SortBy) && !CourseConstants.IsValidSortField(query.SortBy))
        {
            errors[nameof(query.SortBy)] = [$"Invalid sort field '{query.SortBy}'. Allowed values: {string.Join(", ", CourseConstants.AllowedSortFields)}."];
        }

        if (errors.Count > 0)
        {
            _logger.LogWarning("Validation failed for course query parameters: {ErrorCount} errors.", errors.Count);
            throw new ValidationException(errors);
        }
    }

    public async Task ValidateCreateAsync(CreateCourseDto dto, Guid organizationId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        ValidateFieldConstraints(dto.Name, dto.Description, dto.Category, dto.BasePrice, dto.DurationInHours, dto.ThumbnailUrl, dto.Status);

        // 1. Verify Organization exists
        var orgExists = await _dbContext.Organizations
            .IgnoreQueryFilters()
            .AnyAsync(o => o.Id == organizationId, cancellationToken);

        if (!orgExists)
        {
            _logger.LogWarning("Validation failed: Organization '{OrganizationId}' does not exist.", organizationId);
            throw new NotFoundException("Organization", organizationId);
        }

        // 2. Verify Course name uniqueness within the organization
        var trimmedName = dto.Name.Trim();
        var nameExists = await _dbContext.Courses
            .IgnoreQueryFilters()
            .AnyAsync(c => c.OrganizationId == organizationId && c.Name == trimmedName, cancellationToken);

        if (nameExists)
        {
            _logger.LogWarning("Validation failed: Course with name '{CourseName}' already exists in organization '{OrganizationId}'.",
                trimmedName, organizationId);
            throw new ConflictException($"A course named '{trimmedName}' already exists in this organization.");
        }
    }

    public async Task ValidateUpdateAsync(Guid id, UpdateCourseDto dto, Guid organizationId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        ValidateFieldConstraints(dto.Name, dto.Description, dto.Category, dto.BasePrice, dto.DurationInHours, dto.ThumbnailUrl, dto.Status);

        // 1. Verify Course exists
        var course = await _dbContext.Courses
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

        if (course == null)
        {
            _logger.LogWarning("Validation failed: Course '{CourseId}' was not found for update.", id);
            throw new NotFoundException("Course", id);
        }

        // 2. Verify Course name uniqueness within the organization (excluding current course)
        var trimmedName = dto.Name.Trim();
        var nameExists = await _dbContext.Courses
            .IgnoreQueryFilters()
            .AnyAsync(c => c.OrganizationId == organizationId && c.Name == trimmedName && c.Id != id, cancellationToken);

        if (nameExists)
        {
            _logger.LogWarning("Validation failed: Course name '{CourseName}' is already used by another course in organization '{OrganizationId}'.",
                trimmedName, organizationId);
            throw new ConflictException($"A course named '{trimmedName}' already exists in this organization.");
        }
    }

    public async Task ValidateDeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        // 1. Verify Course exists
        var course = await _dbContext.Courses
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

        if (course == null)
        {
            _logger.LogWarning("Validation failed: Course '{CourseId}' was not found for deletion.", id);
            throw new NotFoundException("Course", id);
        }

        // 2. Verify no active dependent records (CourseGroups)
        var courseGroupCount = await _dbContext.CourseGroups
            .IgnoreQueryFilters()
            .CountAsync(cg => cg.CourseId == id, cancellationToken);

        if (courseGroupCount > 0)
        {
            _logger.LogWarning("Validation failed: Cannot delete course '{CourseId}' due to active dependencies ({CourseGroupCount} course groups).",
                id, courseGroupCount);
            throw new ConflictException(
                $"Cannot delete course '{course.Name}' because it has {courseGroupCount} associated course group(s). " +
                "Please remove or archive these groups first.");
        }
    }

    private static void ValidateFieldConstraints(
        string? name,
        string? description,
        string? category,
        decimal basePrice,
        int? durationInHours,
        string? thumbnailUrl,
        byte status)
    {
        if (!CourseConstants.IsValidName(name))
        {
            throw new ValidationException(nameof(name),
                $"Course name must be between {CourseConstants.NameMinLength} and {CourseConstants.NameMaxLength} characters.");
        }

        if (!CourseConstants.IsValidDescription(description))
        {
            throw new ValidationException(nameof(description),
                $"Description cannot exceed {CourseConstants.DescriptionMaxLength} characters.");
        }

        if (!CourseConstants.IsValidCategory(category))
        {
            throw new ValidationException(nameof(category),
                $"Category must be between {CourseConstants.CategoryMinLength} and {CourseConstants.CategoryMaxLength} characters.");
        }

        if (!CourseConstants.IsValidBasePrice(basePrice))
        {
            throw new ValidationException(nameof(basePrice),
                $"Base price must be between {CourseConstants.MinBasePrice} and {CourseConstants.MaxBasePrice}.");
        }

        if (!CourseConstants.IsValidDurationInHours(durationInHours))
        {
            throw new ValidationException(nameof(durationInHours),
                $"Duration in hours must be between {CourseConstants.MinDurationInHours} and {CourseConstants.MaxDurationInHours}.");
        }

        if (!CourseConstants.IsValidThumbnailUrl(thumbnailUrl))
        {
            throw new ValidationException(nameof(thumbnailUrl),
                $"Thumbnail URL is invalid. It must be a valid HTTP or HTTPS URL not exceeding {CourseConstants.ThumbnailUrlMaxLength} characters.");
        }

        if (!CourseConstants.IsValidStatus(status))
        {
            throw new ValidationException(nameof(status),
                $"Invalid status '{status}'. Allowed values are: {string.Join(", ", CourseConstants.Statuses.All)}.");
        }
    }
}
