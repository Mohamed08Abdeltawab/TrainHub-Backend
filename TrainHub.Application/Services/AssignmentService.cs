using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TrainHub.Application.Common.Exceptions;
using TrainHub.Application.Common.Models;
using TrainHub.Application.Features.Assignments.DTOs;
using TrainHub.Application.Interfaces;
using TrainHub.Domain.Constants;
using TrainHub.Domain.Models;

namespace TrainHub.Application.Services;

/// <summary>
/// Service implementation for managing Assignment entities and operations.
/// </summary>
public class AssignmentService : IAssignmentService
{
    private readonly ITrainHubDbContext _dbContext;
    private readonly IAssignmentValidator _assignmentValidator;
    private readonly ICurrentOrganizationService _currentOrganizationService;
    private readonly ILogger<AssignmentService> _logger;

    public AssignmentService(
        ITrainHubDbContext dbContext,
        IAssignmentValidator assignmentValidator,
        ICurrentOrganizationService currentOrganizationService,
        ILogger<AssignmentService> logger)
    {
        _dbContext = dbContext;
        _assignmentValidator = assignmentValidator;
        _currentOrganizationService = currentOrganizationService;
        _logger = logger;
    }

    public async Task<PagedResult<AssignmentDto>> GetPagedAsync(
        AssignmentQueryParameters parameters,
        Guid? currentUserId = null,
        CancellationToken cancellationToken = default)
    {
        parameters ??= new AssignmentQueryParameters();

        _logger.LogInformation("Retrieving paged assignments: Page {PageNumber}, Size {PageSize}, Sort {SortBy} (Desc: {Desc}).",
            parameters.PageNumber, parameters.PageSize, parameters.SortBy, parameters.SortDescending);

        _assignmentValidator.ValidateQuery(parameters);

        var organizationId = _currentOrganizationService.OrganizationId;
        if (!organizationId.HasValue || organizationId.Value == Guid.Empty)
        {
            _logger.LogWarning("Cannot retrieve assignments: Organization ID could not be determined from current context.");
            throw new BadRequestException("Organization context is required.");
        }

        var query = _dbContext.Assignments
            .AsNoTracking()
            .Where(a => a.OrganizationId == organizationId.Value)
            .Include(a => a.CourseGroup)
                .ThenInclude(cg => cg.Course)
            .AsQueryable();

        // If current user is provided and is an instructor, restrict to course groups they teach
        if (currentUserId.HasValue && currentUserId.Value != Guid.Empty && _assignmentValidator is AssignmentValidator concreteValidator)
        {
            var (isOrgStaff, instructor) = await concreteValidator.ResolveCallerPermissionsAsync(currentUserId.Value, organizationId.Value, cancellationToken);
            if (!isOrgStaff)
            {
                if (instructor != null)
                {
                    query = query.Where(a => a.CourseGroup.InstructorId == instructor.Id);
                }
                else
                {
                    // User is not staff and not an instructor
                    query = query.Where(a => false);
                }
            }
        }

        if (!string.IsNullOrWhiteSpace(parameters.SearchTerm))
        {
            var term = parameters.SearchTerm.Trim();
            query = query.Where(a => a.Title.Contains(term) || (a.Description != null && a.Description.Contains(term)));
        }

        if (parameters.CourseGroupId.HasValue)
        {
            query = query.Where(a => a.CourseGroupId == parameters.CourseGroupId.Value);
        }

        if (parameters.CourseId.HasValue)
        {
            query = query.Where(a => a.CourseGroup.CourseId == parameters.CourseId.Value);
        }

        if (parameters.Status.HasValue)
        {
            query = query.Where(a => a.Status == parameters.Status.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var sortBy = (parameters.SortBy ?? "CreatedAt").Trim().ToLowerInvariant();
        var isDescending = parameters.SortDescending;

        query = sortBy switch
        {
            "title" => isDescending ? query.OrderByDescending(a => a.Title).ThenBy(a => a.Id) : query.OrderBy(a => a.Title).ThenBy(a => a.Id),
            "duedate" => isDescending ? query.OrderByDescending(a => a.DueDate).ThenBy(a => a.Id) : query.OrderBy(a => a.DueDate).ThenBy(a => a.Id),
            "status" => isDescending ? query.OrderByDescending(a => a.Status).ThenBy(a => a.Id) : query.OrderBy(a => a.Status).ThenBy(a => a.Id),
            "updatedat" => isDescending ? query.OrderByDescending(a => a.UpdatedAt).ThenBy(a => a.Id) : query.OrderBy(a => a.UpdatedAt).ThenBy(a => a.Id),
            _ => isDescending ? query.OrderByDescending(a => a.CreatedAt).ThenBy(a => a.Id) : query.OrderBy(a => a.CreatedAt).ThenBy(a => a.Id)
        };

        var skip = (parameters.PageNumber - 1) * parameters.PageSize;
        var items = await query
            .Skip(skip)
            .Take(parameters.PageSize)
            .ToListAsync(cancellationToken);

        var dtos = items.Select(MapToDto).ToList();

        return new PagedResult<AssignmentDto>(dtos, totalCount, parameters.PageNumber, parameters.PageSize);
    }

    public async Task<AssignmentDto> GetByIdAsync(Guid id, Guid? currentUserId = null, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Retrieving assignment with ID '{AssignmentId}'.", id);

        var organizationId = _currentOrganizationService.OrganizationId;
        if (!organizationId.HasValue || organizationId.Value == Guid.Empty)
        {
            throw new BadRequestException("Organization context is required.");
        }

        var assignment = await _dbContext.Assignments
            .AsNoTracking()
            .Where(a => a.OrganizationId == organizationId.Value && a.Id == id)
            .Include(a => a.CourseGroup)
                .ThenInclude(cg => cg.Course)
            .FirstOrDefaultAsync(cancellationToken);

        if (assignment == null)
        {
            _logger.LogWarning("Assignment '{AssignmentId}' was not found in organization '{OrgId}'.", id, organizationId.Value);
            throw new NotFoundException("Assignment", id);
        }

        if (currentUserId.HasValue && currentUserId.Value != Guid.Empty && _assignmentValidator is AssignmentValidator concreteValidator)
        {
            var (isOrgStaff, instructor) = await concreteValidator.ResolveCallerPermissionsAsync(currentUserId.Value, organizationId.Value, cancellationToken);
            if (!isOrgStaff)
            {
                if (instructor == null || assignment.CourseGroup.InstructorId != instructor.Id)
                {
                    throw new ForbiddenAccessException("You are not authorized to view this assignment.");
                }
            }
        }

        return MapToDto(assignment);
    }

    public async Task<AssignmentDto> CreateAsync(CreateAssignmentDto dto, Guid? currentUserId = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var organizationId = _currentOrganizationService.OrganizationId;
        if (!organizationId.HasValue || organizationId.Value == Guid.Empty)
        {
            _logger.LogWarning("Cannot create assignment: Organization ID could not be determined from current context.");
            throw new BadRequestException("Organization context is required.");
        }

        _logger.LogInformation("Creating assignment for group '{CourseGroupId}' in org '{OrganizationId}'.",
            dto.CourseGroupId, organizationId.Value);

        await _assignmentValidator.ValidateCreateAsync(dto, organizationId.Value, currentUserId, cancellationToken);

        var assignment = Assignment.Create(
            organizationId.Value,
            dto.CourseGroupId,
            dto.Title,
            dto.DueDate,
            dto.Description,
            dto.MaxScore,
            dto.Status);

        _dbContext.Assignments.Add(assignment);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Assignment '{AssignmentId}' created successfully.", assignment.Id);

        var created = await _dbContext.Assignments
            .AsNoTracking()
            .Include(a => a.CourseGroup)
                .ThenInclude(cg => cg.Course)
            .FirstAsync(a => a.Id == assignment.Id, cancellationToken);

        return MapToDto(created);
    }

    public async Task<AssignmentDto> UpdateAsync(Guid id, UpdateAssignmentDto dto, Guid? currentUserId = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var organizationId = _currentOrganizationService.OrganizationId;
        if (!organizationId.HasValue || organizationId.Value == Guid.Empty)
        {
            _logger.LogWarning("Cannot update assignment: Organization ID could not be determined from current context.");
            throw new BadRequestException("Organization context is required.");
        }

        _logger.LogInformation("Updating assignment '{AssignmentId}' in organization '{OrganizationId}'.", id, organizationId.Value);

        await _assignmentValidator.ValidateUpdateAsync(id, dto, organizationId.Value, currentUserId, cancellationToken);

        var assignment = await _dbContext.Assignments
            .FirstOrDefaultAsync(a => a.Id == id && a.OrganizationId == organizationId.Value, cancellationToken);

        if (assignment == null)
        {
            _logger.LogWarning("Assignment '{AssignmentId}' was not found.", id);
            throw new NotFoundException("Assignment", id);
        }

        assignment.UpdateDetails(
            dto.Title,
            dto.DueDate,
            dto.Description,
            dto.MaxScore,
            dto.Status);

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Assignment '{AssignmentId}' updated successfully.", id);

        var updated = await _dbContext.Assignments
            .AsNoTracking()
            .Include(a => a.CourseGroup)
                .ThenInclude(cg => cg.Course)
            .FirstAsync(a => a.Id == id, cancellationToken);

        return MapToDto(updated);
    }

    public async Task DeleteAsync(Guid id, Guid? currentUserId = null, CancellationToken cancellationToken = default)
    {
        var organizationId = _currentOrganizationService.OrganizationId;
        if (!organizationId.HasValue || organizationId.Value == Guid.Empty)
        {
            _logger.LogWarning("Cannot delete assignment: Organization ID could not be determined from current context.");
            throw new BadRequestException("Organization context is required.");
        }

        _logger.LogInformation("Deleting assignment '{AssignmentId}' from organization '{OrganizationId}'.", id, organizationId.Value);

        await _assignmentValidator.ValidateDeleteAsync(id, organizationId.Value, currentUserId, cancellationToken);

        var assignment = await _dbContext.Assignments
            .FirstOrDefaultAsync(a => a.Id == id && a.OrganizationId == organizationId.Value, cancellationToken);

        if (assignment == null)
        {
            _logger.LogWarning("Assignment '{AssignmentId}' was not found.", id);
            throw new NotFoundException("Assignment", id);
        }

        _dbContext.Assignments.Remove(assignment);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Assignment '{AssignmentId}' deleted successfully.", id);
    }

    private static AssignmentDto MapToDto(Assignment a)
    {
        return new AssignmentDto
        {
            Id = a.Id,
            OrganizationId = a.OrganizationId,
            CourseGroupId = a.CourseGroupId,
            CourseGroupName = a.CourseGroup?.Name ?? string.Empty,
            CourseId = a.CourseGroup?.CourseId ?? Guid.Empty,
            CourseName = a.CourseGroup?.Course?.Name ?? string.Empty,
            Title = a.Title,
            Description = a.Description,
            DueDate = a.DueDate,
            MaxScore = a.MaxScore,
            Status = a.Status,
            StatusName = AssignmentConstants.Statuses.GetName(a.Status),
            CreatedAt = a.CreatedAt,
            UpdatedAt = a.UpdatedAt
        };
    }
}
