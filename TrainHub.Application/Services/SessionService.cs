using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TrainHub.Application.Common.Exceptions;
using TrainHub.Application.Common.Models;
using TrainHub.Application.Features.Sessions.DTOs;
using TrainHub.Application.Interfaces;
using TrainHub.Domain.Constants;
using TrainHub.Domain.Models;

namespace TrainHub.Application.Services;

/// <summary>
/// Service implementation for managing Session entities and operations.
/// </summary>
public class SessionService : ISessionService
{
    private readonly ITrainHubDbContext _dbContext;
    private readonly ISessionValidator _sessionValidator;
    private readonly ICurrentOrganizationService _currentOrganizationService;
    private readonly ILogger<SessionService> _logger;

    public SessionService(
        ITrainHubDbContext dbContext,
        ISessionValidator sessionValidator,
        ICurrentOrganizationService currentOrganizationService,
        ILogger<SessionService> logger)
    {
        _dbContext = dbContext;
        _sessionValidator = sessionValidator;
        _currentOrganizationService = currentOrganizationService;
        _logger = logger;
    }

    public async Task<PagedResult<SessionDto>> GetPagedAsync(SessionQueryParameters parameters, CancellationToken cancellationToken = default)
    {
        parameters ??= new SessionQueryParameters();

        _logger.LogInformation("Retrieving paged sessions: Page {PageNumber}, Size {PageSize}, Sort {SortBy} (Desc: {Desc}).",
            parameters.PageNumber, parameters.PageSize, parameters.SortBy, parameters.SortDescending);

        _sessionValidator.ValidateQuery(parameters);

        var query = _dbContext.Sessions
            .AsNoTracking()
            .Include(s => s.CourseGroup)
                .ThenInclude(cg => cg.Course)
            .Include(s => s.Instructor)
                .ThenInclude(i => i!.User)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(parameters.SearchTerm))
        {
            var term = parameters.SearchTerm.Trim();
            query = query.Where(s =>
                (s.Room != null && s.Room.Contains(term)) ||
                s.CourseGroup.Name.Contains(term) ||
                s.CourseGroup.Course.Name.Contains(term));
        }

        if (parameters.CourseGroupId.HasValue)
        {
            query = query.Where(s => s.CourseGroupId == parameters.CourseGroupId.Value);
        }

        if (parameters.CourseId.HasValue)
        {
            query = query.Where(s => s.CourseGroup.CourseId == parameters.CourseId.Value);
        }

        if (parameters.InstructorId.HasValue)
        {
            query = query.Where(s => s.InstructorId == parameters.InstructorId.Value);
        }

        if (parameters.Status.HasValue)
        {
            query = query.Where(s => s.Status == parameters.Status.Value);
        }

        if (parameters.FromDate.HasValue)
        {
            query = query.Where(s => s.StartTime >= parameters.FromDate.Value);
        }

        if (parameters.ToDate.HasValue)
        {
            query = query.Where(s => s.StartTime <= parameters.ToDate.Value);
        }

        if (!string.IsNullOrWhiteSpace(parameters.Room))
        {
            var room = parameters.Room.Trim();
            query = query.Where(s => s.Room != null && s.Room.Contains(room));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var isDescending = parameters.SortDescending;
        query = (parameters.SortBy?.Trim().ToLowerInvariant()) switch
        {
            "endtime" => isDescending ? query.OrderByDescending(s => s.EndTime).ThenBy(s => s.Id) : query.OrderBy(s => s.EndTime).ThenBy(s => s.Id),
            "room" => isDescending ? query.OrderByDescending(s => s.Room).ThenBy(s => s.Id) : query.OrderBy(s => s.Room).ThenBy(s => s.Id),
            "status" => isDescending ? query.OrderByDescending(s => s.Status).ThenBy(s => s.Id) : query.OrderBy(s => s.Status).ThenBy(s => s.Id),
            "createdat" => isDescending ? query.OrderByDescending(s => s.CreatedAt).ThenBy(s => s.Id) : query.OrderBy(s => s.CreatedAt).ThenBy(s => s.Id),
            "updatedat" => isDescending ? query.OrderByDescending(s => s.UpdatedAt).ThenBy(s => s.Id) : query.OrderBy(s => s.UpdatedAt).ThenBy(s => s.Id),
            _ => isDescending ? query.OrderByDescending(s => s.StartTime).ThenBy(s => s.Id) : query.OrderBy(s => s.StartTime).ThenBy(s => s.Id)
        };

        var skip = (parameters.PageNumber - 1) * parameters.PageSize;
        var items = await query
            .Skip(skip)
            .Take(parameters.PageSize)
            .ToListAsync(cancellationToken);

        var dtos = items.Select(s => MapToDto(s)).ToList();

        return new PagedResult<SessionDto>(dtos, totalCount, parameters.PageNumber, parameters.PageSize);
    }

    public async Task<SessionDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Retrieving session with ID '{SessionId}'.", id);

        var session = await _dbContext.Sessions
            .AsNoTracking()
            .Include(s => s.CourseGroup)
                .ThenInclude(cg => cg.Course)
            .Include(s => s.Instructor)
                .ThenInclude(i => i!.User)
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

        if (session == null)
        {
            _logger.LogWarning("Session '{SessionId}' was not found.", id);
            throw new NotFoundException("Session", id);
        }

        return MapToDto(session);
    }

    public async Task<IReadOnlyList<SessionDto>> GetByCourseGroupIdAsync(Guid courseGroupId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Retrieving sessions for course group '{CourseGroupId}'.", courseGroupId);

        var organizationId = _currentOrganizationService.OrganizationId;
        if (!organizationId.HasValue || organizationId.Value == Guid.Empty)
        {
            _logger.LogWarning("Cannot retrieve course group sessions: Organization ID could not be determined from current context.");
            throw new BadRequestException("Organization context is required.");
        }

        var groupExists = await _dbContext.CourseGroups
            .IgnoreQueryFilters()
            .AnyAsync(cg => cg.Id == courseGroupId && cg.OrganizationId == organizationId.Value, cancellationToken);

        if (!groupExists)
        {
            _logger.LogWarning("Course group '{CourseGroupId}' was not found in organization '{OrganizationId}'.", courseGroupId, organizationId.Value);
            throw new NotFoundException("CourseGroup", courseGroupId);
        }

        var sessions = await _dbContext.Sessions
            .AsNoTracking()
            .Where(s => s.CourseGroupId == courseGroupId && s.OrganizationId == organizationId.Value)
            .Include(s => s.CourseGroup)
                .ThenInclude(cg => cg.Course)
            .Include(s => s.Instructor)
                .ThenInclude(i => i!.User)
            .OrderBy(s => s.StartTime)
            .ThenBy(s => s.Id)
            .ToListAsync(cancellationToken);

        var dtos = sessions.Select(s => MapToDto(s)).ToList();

        _logger.LogInformation("Retrieved {Count} session(s) for course group '{CourseGroupId}'.", dtos.Count, courseGroupId);

        return dtos;
    }

    public async Task<SessionDto> CreateAsync(CreateSessionDto dto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var organizationId = _currentOrganizationService.OrganizationId;
        if (!organizationId.HasValue || organizationId.Value == Guid.Empty)
        {
            _logger.LogWarning("Cannot create session: Organization ID could not be determined from current context.");
            throw new BadRequestException("Organization context is required.");
        }

        _logger.LogInformation("Creating session for group '{CourseGroupId}' in org '{OrganizationId}'.",
            dto.CourseGroupId, organizationId.Value);

        await _sessionValidator.ValidateCreateAsync(dto, organizationId.Value, cancellationToken);

        var courseGroup = await _dbContext.CourseGroups
            .IgnoreQueryFilters()
            .FirstAsync(cg => cg.Id == dto.CourseGroupId && cg.OrganizationId == organizationId.Value, cancellationToken);

        var instructorId = dto.InstructorId ?? courseGroup.InstructorId;

        var session = Session.Create(
            organizationId.Value,
            dto.CourseGroupId,
            dto.StartTime,
            dto.EndTime,
            dto.Room,
            instructorId,
            dto.Status);

        _dbContext.Sessions.Add(session);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Session '{SessionId}' created successfully.", session.Id);

        var created = await _dbContext.Sessions
            .AsNoTracking()
            .Include(s => s.CourseGroup)
                .ThenInclude(cg => cg.Course)
            .Include(s => s.Instructor)
                .ThenInclude(i => i!.User)
            .FirstAsync(s => s.Id == session.Id, cancellationToken);

        return MapToDto(created);
    }

    public async Task<SessionDto> UpdateAsync(Guid id, UpdateSessionDto dto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var organizationId = _currentOrganizationService.OrganizationId;
        if (!organizationId.HasValue || organizationId.Value == Guid.Empty)
        {
            _logger.LogWarning("Cannot update session: Organization ID could not be determined from current context.");
            throw new BadRequestException("Organization context is required.");
        }

        _logger.LogInformation("Updating session '{SessionId}' in organization '{OrganizationId}'.", id, organizationId.Value);

        await _sessionValidator.ValidateUpdateAsync(id, dto, organizationId.Value, cancellationToken);

        var session = await _dbContext.Sessions
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

        if (session == null)
        {
            _logger.LogWarning("Session '{SessionId}' was not found.", id);
            throw new NotFoundException("Session", id);
        }

        session.UpdateDetails(
            dto.StartTime,
            dto.EndTime,
            dto.Room,
            dto.InstructorId,
            dto.Status);

        _dbContext.Sessions.Update(session);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Session '{SessionId}' updated successfully.", id);

        var updated = await _dbContext.Sessions
            .AsNoTracking()
            .Include(s => s.CourseGroup)
                .ThenInclude(cg => cg.Course)
            .Include(s => s.Instructor)
                .ThenInclude(i => i!.User)
            .FirstAsync(s => s.Id == id, cancellationToken);

        return MapToDto(updated);
    }

    public async Task<SessionDto> UpdateStatusAsync(Guid id, UpdateSessionStatusDto dto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        _logger.LogInformation("Updating status of session '{SessionId}' to '{Status}'.", id, dto.Status);

        await _sessionValidator.ValidateUpdateStatusAsync(id, dto.Status, cancellationToken);

        var session = await _dbContext.Sessions
            .Include(s => s.CourseGroup)
                .ThenInclude(cg => cg.Course)
            .Include(s => s.Instructor)
                .ThenInclude(i => i!.User)
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

        if (session == null)
        {
            _logger.LogWarning("Session '{SessionId}' was not found.", id);
            throw new NotFoundException("Session", id);
        }

        session.UpdateStatus(dto.Status);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Status of session '{SessionId}' updated to '{Status}'.", id, dto.Status);

        return MapToDto(session);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Deleting session '{SessionId}'.", id);

        await _sessionValidator.ValidateDeleteAsync(id, cancellationToken);

        var session = await _dbContext.Sessions
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

        if (session == null)
        {
            _logger.LogWarning("Session '{SessionId}' was not found for deletion.", id);
            throw new NotFoundException("Session", id);
        }

        _dbContext.Sessions.Remove(session);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Session '{SessionId}' deleted successfully.", id);
    }

    private static SessionDto MapToDto(Session session)
    {
        var group = session.CourseGroup;
        var course = group?.Course;
        var instructor = session.Instructor;
        var instructorUser = instructor?.User;

        var instructorName = instructorUser != null
            ? $"{instructorUser.FirstName} {instructorUser.LastName}".Trim()
            : null;

        return new SessionDto
        {
            Id = session.Id,
            OrganizationId = session.OrganizationId,
            CourseGroupId = session.CourseGroupId,
            CourseGroupName = group?.Name ?? string.Empty,
            CourseId = course?.Id ?? (group != null ? group.CourseId : Guid.Empty),
            CourseName = course?.Name ?? string.Empty,
            InstructorId = session.InstructorId,
            InstructorName = instructorName,
            StartTime = session.StartTime,
            EndTime = session.EndTime,
            Room = session.Room,
            Status = session.Status,
            StatusName = SessionConstants.Statuses.GetName(session.Status),
            CreatedAt = session.CreatedAt,
            UpdatedAt = session.UpdatedAt
        };
    }
}
