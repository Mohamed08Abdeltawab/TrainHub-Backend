using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TrainHub.Application.Common.Exceptions;
using TrainHub.Application.Common.Models;
using TrainHub.Application.Features.Courses.DTOs;
using TrainHub.Application.Interfaces;
using TrainHub.Domain.Constants;
using TrainHub.Domain.Models;
using TrainHub.Infrastructure.Data;

namespace TrainHub.Infrastructure.Services;

/// <summary>
/// Service implementation for managing Course entities and operations.
/// </summary>
public class CourseService : ICourseService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICourseValidator _courseValidator;
    private readonly ICurrentOrganizationService _currentOrganizationService;
    private readonly TrainHubDbContext _dbContext;
    private readonly ILogger<CourseService> _logger;

    public CourseService(
        IUnitOfWork unitOfWork,
        ICourseValidator courseValidator,
        ICurrentOrganizationService currentOrganizationService,
        TrainHubDbContext dbContext,
        ILogger<CourseService> logger)
    {
        _unitOfWork = unitOfWork;
        _courseValidator = courseValidator;
        _currentOrganizationService = currentOrganizationService;
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<PagedResult<CourseDto>> GetPagedAsync(CourseQueryParameters parameters, CancellationToken cancellationToken = default)
    {
        parameters ??= new CourseQueryParameters();

        _logger.LogInformation("Retrieving paged courses: Page {PageNumber}, Size {PageSize}, Sort {SortBy} (Desc: {Desc}).",
            parameters.PageNumber, parameters.PageSize, parameters.SortBy, parameters.SortDescending);

        // 1. Validate query parameters
        _courseValidator.ValidateQuery(parameters);

        // 2. Base query with CourseGroups (tenant filter applied automatically)
        var query = _dbContext.Courses
            .AsNoTracking()
            .Include(c => c.CourseGroups)
            .AsQueryable();

        // 3. Search filter
        if (!string.IsNullOrWhiteSpace(parameters.SearchTerm))
        {
            var term = parameters.SearchTerm.Trim();
            query = query.Where(c =>
                c.Name.Contains(term) ||
                (c.Description != null && c.Description.Contains(term)) ||
                (c.Category != null && c.Category.Contains(term)));
        }

        // 4. Category filter
        if (!string.IsNullOrWhiteSpace(parameters.Category))
        {
            var category = parameters.Category.Trim();
            query = query.Where(c => c.Category == category);
        }

        // 5. Status filter
        if (parameters.Status.HasValue)
        {
            query = query.Where(c => c.Status == parameters.Status.Value);
        }

        // 6. BasePrice range filters
        if (parameters.MinPrice.HasValue)
        {
            query = query.Where(c => c.BasePrice >= parameters.MinPrice.Value);
        }

        if (parameters.MaxPrice.HasValue)
        {
            query = query.Where(c => c.BasePrice <= parameters.MaxPrice.Value);
        }

        // 7. Duration range filters
        if (parameters.MinDuration.HasValue)
        {
            query = query.Where(c => c.DurationInHours.HasValue && c.DurationInHours.Value >= parameters.MinDuration.Value);
        }

        if (parameters.MaxDuration.HasValue)
        {
            query = query.Where(c => c.DurationInHours.HasValue && c.DurationInHours.Value <= parameters.MaxDuration.Value);
        }

        // 8. Total count before pagination
        var totalCount = await query.CountAsync(cancellationToken);

        // 9. Sorting
        var isDescending = parameters.SortDescending;
        query = (parameters.SortBy?.Trim().ToLowerInvariant()) switch
        {
            "name" => isDescending ? query.OrderByDescending(c => c.Name) : query.OrderBy(c => c.Name),
            "category" => isDescending ? query.OrderByDescending(c => c.Category) : query.OrderBy(c => c.Category),
            "baseprice" => isDescending ? query.OrderByDescending(c => c.BasePrice) : query.OrderBy(c => c.BasePrice),
            "durationinhours" => isDescending ? query.OrderByDescending(c => c.DurationInHours) : query.OrderBy(c => c.DurationInHours),
            "status" => isDescending ? query.OrderByDescending(c => c.Status) : query.OrderBy(c => c.Status),
            "updatedat" => isDescending ? query.OrderByDescending(c => c.UpdatedAt) : query.OrderBy(c => c.UpdatedAt),
            _ => isDescending ? query.OrderByDescending(c => c.CreatedAt) : query.OrderBy(c => c.CreatedAt)
        };

        // 10. Pagination
        var skip = (parameters.PageNumber - 1) * parameters.PageSize;
        var items = await query
            .Skip(skip)
            .Take(parameters.PageSize)
            .ToListAsync(cancellationToken);

        var dtos = items.Select(MapToDto).ToList();

        return new PagedResult<CourseDto>(dtos, totalCount, parameters.PageNumber, parameters.PageSize);
    }

    public async Task<CourseDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Retrieving course with ID '{CourseId}'.", id);

        var course = await _dbContext.Courses
            .AsNoTracking()
            .Include(c => c.CourseGroups)
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

        if (course == null)
        {
            _logger.LogWarning("Course '{CourseId}' was not found.", id);
            throw new NotFoundException("Course", id);
        }

        return MapToDto(course);
    }

    public async Task<IEnumerable<CourseDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Retrieving all courses within current tenant scope.");

        var courses = await _dbContext.Courses
            .AsNoTracking()
            .Include(c => c.CourseGroups)
            .ToListAsync(cancellationToken);

        return courses.Select(MapToDto).ToList();
    }

    public async Task<CourseDto> CreateAsync(CreateCourseDto dto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var organizationId = dto.OrganizationId ?? _currentOrganizationService.OrganizationId;
        if (!organizationId.HasValue || organizationId.Value == Guid.Empty)
        {
            _logger.LogWarning("Cannot create course: Organization ID could not be determined.");
            throw new BadRequestException("Organization ID must be provided or available in the current context.");
        }

        _logger.LogInformation("Creating course '{CourseName}' in organization '{OrganizationId}'.",
            dto.Name, organizationId.Value);

        // Enforce business rules and uniqueness
        await _courseValidator.ValidateCreateAsync(dto, organizationId.Value, cancellationToken);

        var course = Course.Create(
            organizationId.Value,
            dto.Name,
            dto.Description,
            dto.Category,
            dto.BasePrice,
            dto.DurationInHours,
            dto.ThumbnailUrl,
            dto.Status);

        await _unitOfWork.Repository<Course>().AddAsync(course, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Course '{CourseId}' created successfully.", course.Id);

        return MapToDto(course);
    }

    public async Task<CourseDto> UpdateAsync(Guid id, UpdateCourseDto dto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        _logger.LogInformation("Updating course '{CourseId}'.", id);

        var course = await _dbContext.Courses
            .Include(c => c.CourseGroups)
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

        if (course == null)
        {
            _logger.LogWarning("Course '{CourseId}' was not found for update.", id);
            throw new NotFoundException("Course", id);
        }

        // Enforce business rules and uniqueness
        await _courseValidator.ValidateUpdateAsync(id, dto, course.OrganizationId, cancellationToken);

        course.UpdateDetails(
            dto.Name,
            dto.Description,
            dto.Category,
            dto.BasePrice,
            dto.DurationInHours,
            dto.ThumbnailUrl,
            dto.Status);

        _unitOfWork.Repository<Course>().Update(course);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Course '{CourseId}' updated successfully.", id);

        return MapToDto(course);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Deleting course '{CourseId}'.", id);

        // Enforce dependency and existence rules
        await _courseValidator.ValidateDeleteAsync(id, cancellationToken);

        var course = await _unitOfWork.Repository<Course>().GetByIdAsync(id, cancellationToken);
        if (course == null)
        {
            _logger.LogWarning("Course '{CourseId}' was not found for deletion.", id);
            throw new NotFoundException("Course", id);
        }

        _unitOfWork.Repository<Course>().Delete(course);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Course '{CourseId}' deleted successfully.", id);
    }

    private static CourseDto MapToDto(Course course)
    {
        return new CourseDto
        {
            Id = course.Id,
            OrganizationId = course.OrganizationId,
            Name = course.Name,
            Description = course.Description,
            Category = course.Category,
            BasePrice = course.BasePrice,
            DurationInHours = course.DurationInHours,
            ThumbnailUrl = course.ThumbnailUrl,
            Status = course.Status,
            StatusName = CourseConstants.Statuses.GetName(course.Status),
            CreatedAt = course.CreatedAt,
            UpdatedAt = course.UpdatedAt,
            CourseGroupsCount = course.CourseGroups?.Count ?? 0
        };
    }
}
