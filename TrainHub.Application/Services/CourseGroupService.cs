using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TrainHub.Application.Common.Exceptions;
using TrainHub.Application.Common.Models;
using TrainHub.Application.Features.CourseGroups.DTOs;
using TrainHub.Application.Interfaces;
using TrainHub.Domain.Constants;
using TrainHub.Domain.Models;

namespace TrainHub.Application.Services;

/// <summary>
/// Service implementation for managing CourseGroup entities and operations.
/// </summary>
public class CourseGroupService : ICourseGroupService
{
    private readonly ITrainHubDbContext _dbContext;
    private readonly ICourseGroupValidator _courseGroupValidator;
    private readonly ICurrentOrganizationService _currentOrganizationService;
    private readonly ILogger<CourseGroupService> _logger;

    public CourseGroupService(
        ITrainHubDbContext dbContext,
        ICourseGroupValidator courseGroupValidator,
        ICurrentOrganizationService currentOrganizationService,
        ILogger<CourseGroupService> logger)
    {
        _dbContext = dbContext;
        _courseGroupValidator = courseGroupValidator;
        _currentOrganizationService = currentOrganizationService;
        _logger = logger;
    }

    public async Task<PagedResult<CourseGroupDto>> GetPagedAsync(CourseGroupQueryParameters parameters, CancellationToken cancellationToken = default)
    {
        parameters ??= new CourseGroupQueryParameters();

        _logger.LogInformation("Retrieving paged course groups: Page {PageNumber}, Size {PageSize}, Sort {SortBy} (Desc: {Desc}).",
            parameters.PageNumber, parameters.PageSize, parameters.SortBy, parameters.SortDescending);

        // 1. Validate query parameters
        _courseGroupValidator.ValidateQuery(parameters);

        // 2. Base query with Course and Instructor navigation details (tenant filter applied automatically)
        var query = _dbContext.CourseGroups
            .AsNoTracking()
            .Include(cg => cg.Course)
            .Include(cg => cg.Instructor)
                .ThenInclude(i => i!.User)
            .AsQueryable();

        // 3. Search filter
        if (!string.IsNullOrWhiteSpace(parameters.SearchTerm))
        {
            var term = parameters.SearchTerm.Trim();
            query = query.Where(cg =>
                cg.Name.Contains(term) ||
                (cg.ScheduleDescription != null && cg.ScheduleDescription.Contains(term)));
        }

        // 4. Course filter
        if (parameters.CourseId.HasValue)
        {
            query = query.Where(cg => cg.CourseId == parameters.CourseId.Value);
        }

        // 5. Instructor filter
        if (parameters.InstructorId.HasValue)
        {
            query = query.Where(cg => cg.InstructorId == parameters.InstructorId.Value);
        }

        // 6. Total count before pagination
        var totalCount = await query.CountAsync(cancellationToken);

        // 7. Sorting
        var isDescending = parameters.SortDescending;
        query = (parameters.SortBy?.Trim().ToLowerInvariant()) switch
        {
            "name" => isDescending ? query.OrderByDescending(cg => cg.Name).ThenBy(cg => cg.Id) : query.OrderBy(cg => cg.Name).ThenBy(cg => cg.Id),
            "startdate" => isDescending ? query.OrderByDescending(cg => cg.StartDate).ThenBy(cg => cg.Id) : query.OrderBy(cg => cg.StartDate).ThenBy(cg => cg.Id),
            "enddate" => isDescending ? query.OrderByDescending(cg => cg.EndDate).ThenBy(cg => cg.Id) : query.OrderBy(cg => cg.EndDate).ThenBy(cg => cg.Id),
            "updatedat" => isDescending ? query.OrderByDescending(cg => cg.UpdatedAt).ThenBy(cg => cg.Id) : query.OrderBy(cg => cg.UpdatedAt).ThenBy(cg => cg.Id),
            _ => isDescending ? query.OrderByDescending(cg => cg.CreatedAt).ThenBy(cg => cg.Id) : query.OrderBy(cg => cg.CreatedAt).ThenBy(cg => cg.Id)
        };

        // 8. Pagination
        var skip = (parameters.PageNumber - 1) * parameters.PageSize;
        var items = await query
            .Skip(skip)
            .Take(parameters.PageSize)
            .ToListAsync(cancellationToken);

        var dtos = items.Select(cg => MapToDto(cg)).ToList();

        return new PagedResult<CourseGroupDto>(dtos, totalCount, parameters.PageNumber, parameters.PageSize);
    }

    public async Task<CourseGroupDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Retrieving course group with ID '{CourseGroupId}'.", id);

        var group = await _dbContext.CourseGroups
            .AsNoTracking()
            .Include(cg => cg.Course)
            .Include(cg => cg.Instructor)
                .ThenInclude(i => i!.User)
            .FirstOrDefaultAsync(cg => cg.Id == id, cancellationToken);

        if (group == null)
        {
            _logger.LogWarning("Course group '{CourseGroupId}' was not found.", id);
            throw new NotFoundException("CourseGroup", id);
        }

        return MapToDto(group);
    }

    public async Task<CourseGroupDto> CreateAsync(CreateCourseGroupDto dto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var organizationId = _currentOrganizationService.OrganizationId;
        if (!organizationId.HasValue || organizationId.Value == Guid.Empty)
        {
            _logger.LogWarning("Cannot create course group: Organization ID could not be determined from current context.");
            throw new BadRequestException("Organization context is required.");
        }

        _logger.LogInformation("Creating course group '{GroupName}' for course '{CourseId}' in organization '{OrganizationId}'.",
            dto.Name, dto.CourseId, organizationId.Value);

        await _courseGroupValidator.ValidateCreateAsync(dto, organizationId.Value, cancellationToken);

        var group = CourseGroup.Create(
            organizationId.Value,
            dto.CourseId,
            dto.Name,
            dto.StartDate,
            dto.EndDate,
            dto.ScheduleDescription,
            dto.InstructorId,
            dto.Capacity ?? CourseGroupConstants.DefaultCapacity);

        await _dbContext.CourseGroups.AddAsync(group, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Course group '{CourseGroupId}' created successfully.", group.Id);

        var course = await _dbContext.Courses.FirstOrDefaultAsync(c => c.Id == group.CourseId, cancellationToken);
        Instructor? instructor = null;
        if (group.InstructorId.HasValue)
        {
            instructor = await _dbContext.Instructors
                .Include(i => i.User)
                .FirstOrDefaultAsync(i => i.Id == group.InstructorId.Value, cancellationToken);
        }

        return MapToDto(group, course, instructor);
    }

    public async Task<CourseGroupDto> UpdateAsync(Guid id, UpdateCourseGroupDto dto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        _logger.LogInformation("Updating course group '{CourseGroupId}'.", id);

        var group = await _dbContext.CourseGroups
            .FirstOrDefaultAsync(cg => cg.Id == id, cancellationToken);

        if (group == null)
        {
            _logger.LogWarning("Course group '{CourseGroupId}' was not found for update.", id);
            throw new NotFoundException("CourseGroup", id);
        }

        await _courseGroupValidator.ValidateUpdateAsync(id, dto, group.OrganizationId, cancellationToken);

        group.UpdateDetails(
            dto.Name,
            dto.StartDate,
            dto.EndDate,
            dto.ScheduleDescription,
            dto.InstructorId,
            dto.Capacity);

        _dbContext.CourseGroups.Update(group);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Course group '{CourseGroupId}' updated successfully.", id);

        var course = await _dbContext.Courses.FirstOrDefaultAsync(c => c.Id == group.CourseId, cancellationToken);
        Instructor? instructor = null;
        if (group.InstructorId.HasValue)
        {
            instructor = await _dbContext.Instructors
                .Include(i => i.User)
                .FirstOrDefaultAsync(i => i.Id == group.InstructorId.Value, cancellationToken);
        }

        return MapToDto(group, course, instructor);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Deleting course group '{CourseGroupId}'.", id);

        await _courseGroupValidator.ValidateDeleteAsync(id, cancellationToken);

        var group = await _dbContext.CourseGroups
            .FirstOrDefaultAsync(cg => cg.Id == id, cancellationToken);

        if (group == null)
        {
            _logger.LogWarning("Course group '{CourseGroupId}' was not found for deletion.", id);
            throw new NotFoundException("CourseGroup", id);
        }

        _dbContext.CourseGroups.Remove(group);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Course group '{CourseGroupId}' deleted successfully.", id);
    }

    private static CourseGroupDto MapToDto(CourseGroup group, Course? course = null, Instructor? instructor = null)
    {
        var resolvedCourse = course ?? group.Course;
        var resolvedInstructor = instructor ?? group.Instructor;
        var instructorName = resolvedInstructor?.User != null
            ? $"{resolvedInstructor.User.FirstName} {resolvedInstructor.User.LastName}".Trim()
            : null;

        return new CourseGroupDto
        {
            Id = group.Id,
            OrganizationId = group.OrganizationId,
            CourseId = group.CourseId,
            CourseName = resolvedCourse?.Name ?? string.Empty,
            InstructorId = group.InstructorId,
            InstructorName = instructorName,
            Name = group.Name,
            ScheduleDescription = group.ScheduleDescription,
            StartDate = group.StartDate,
            EndDate = group.EndDate,
            Capacity = group.Capacity,
            Status = group.Status,
            CreatedAt = group.CreatedAt,
            UpdatedAt = group.UpdatedAt
        };
    }
}
