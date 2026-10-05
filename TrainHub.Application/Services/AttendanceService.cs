using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TrainHub.Application.Common.Exceptions;
using TrainHub.Application.Common.Models;
using TrainHub.Application.Features.Attendances.DTOs;
using TrainHub.Application.Interfaces;
using TrainHub.Domain.Constants;
using TrainHub.Domain.Models;

namespace TrainHub.Application.Services;

/// <summary>
/// Service implementation for managing Attendance records and operations.
/// </summary>
public class AttendanceService : IAttendanceService
{
    private readonly ITrainHubDbContext _dbContext;
    private readonly IAttendanceValidator _attendanceValidator;
    private readonly ICurrentOrganizationService _currentOrganizationService;
    private readonly ILogger<AttendanceService> _logger;

    public AttendanceService(
        ITrainHubDbContext dbContext,
        IAttendanceValidator attendanceValidator,
        ICurrentOrganizationService currentOrganizationService,
        ILogger<AttendanceService> logger)
    {
        _dbContext = dbContext;
        _attendanceValidator = attendanceValidator;
        _currentOrganizationService = currentOrganizationService;
        _logger = logger;
    }

    public async Task<PagedResult<AttendanceDto>> GetPagedAsync(AttendanceQueryParameters parameters, CancellationToken cancellationToken = default)
    {
        parameters ??= new AttendanceQueryParameters();

        _logger.LogInformation("Retrieving paged attendance records: Page {PageNumber}, Size {PageSize}, Sort {SortBy} (Desc: {Desc}).",
            parameters.PageNumber, parameters.PageSize, parameters.SortBy, parameters.SortDescending);

        _attendanceValidator.ValidateQuery(parameters);

        var query = _dbContext.AttendanceRecords
            .AsNoTracking()
            .Include(a => a.Student)
                .ThenInclude(s => s.User)
            .Include(a => a.MarkedByUser)
            .Include(a => a.Session)
            .AsQueryable();

        if (parameters.SessionId.HasValue)
        {
            query = query.Where(a => a.SessionId == parameters.SessionId.Value);
        }

        if (parameters.StudentId.HasValue)
        {
            query = query.Where(a => a.StudentId == parameters.StudentId.Value);
        }

        if (parameters.CourseGroupId.HasValue)
        {
            query = query.Where(a => a.Session.CourseGroupId == parameters.CourseGroupId.Value);
        }

        if (parameters.Status.HasValue)
        {
            query = query.Where(a => a.Status == parameters.Status.Value);
        }

        if (parameters.FromDate.HasValue)
        {
            query = query.Where(a => a.MarkedAt >= parameters.FromDate.Value);
        }

        if (parameters.ToDate.HasValue)
        {
            query = query.Where(a => a.MarkedAt <= parameters.ToDate.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var isDescending = parameters.SortDescending;
        query = (parameters.SortBy?.Trim().ToLowerInvariant()) switch
        {
            "status" => isDescending ? query.OrderByDescending(a => a.Status).ThenBy(a => a.Id) : query.OrderBy(a => a.Status).ThenBy(a => a.Id),
            "studentid" => isDescending ? query.OrderByDescending(a => a.StudentId).ThenBy(a => a.Id) : query.OrderBy(a => a.StudentId).ThenBy(a => a.Id),
            "sessionid" => isDescending ? query.OrderByDescending(a => a.SessionId).ThenBy(a => a.Id) : query.OrderBy(a => a.SessionId).ThenBy(a => a.Id),
            _ => isDescending ? query.OrderByDescending(a => a.MarkedAt).ThenBy(a => a.Id) : query.OrderBy(a => a.MarkedAt).ThenBy(a => a.Id)
        };

        var skip = (parameters.PageNumber - 1) * parameters.PageSize;
        var items = await query
            .Skip(skip)
            .Take(parameters.PageSize)
            .ToListAsync(cancellationToken);

        var dtos = items.Select(a => MapToDto(a)).ToList();

        return new PagedResult<AttendanceDto>(dtos, totalCount, parameters.PageNumber, parameters.PageSize);
    }

    public async Task<AttendanceDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Retrieving attendance record with ID '{Id}'.", id);

        var record = await _dbContext.AttendanceRecords
            .AsNoTracking()
            .Include(a => a.Student)
                .ThenInclude(s => s.User)
            .Include(a => a.MarkedByUser)
            .Include(a => a.Session)
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken);

        if (record == null)
        {
            _logger.LogWarning("Attendance record '{Id}' was not found.", id);
            throw new NotFoundException("AttendanceRecord", id);
        }

        return MapToDto(record);
    }

    public async Task<IReadOnlyList<AttendanceDto>> GetBySessionIdAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        var organizationId = _currentOrganizationService.OrganizationId;
        if (!organizationId.HasValue || organizationId.Value == Guid.Empty)
        {
            _logger.LogWarning("Cannot retrieve attendance records: Organization ID could not be determined from current context.");
            throw new BadRequestException("Organization context is required.");
        }

        var sessionExists = await _dbContext.Sessions
            .AsNoTracking()
            .AnyAsync(s => s.Id == sessionId && s.OrganizationId == organizationId.Value, cancellationToken);

        if (!sessionExists)
        {
            _logger.LogWarning("Session '{SessionId}' was not found in organization '{OrganizationId}'.", sessionId, organizationId.Value);
            throw new NotFoundException("Session", sessionId);
        }

        var records = await _dbContext.AttendanceRecords
            .AsNoTracking()
            .Where(a => a.SessionId == sessionId && a.OrganizationId == organizationId.Value)
            .Include(a => a.Student)
                .ThenInclude(s => s.User)
            .Include(a => a.MarkedByUser)
            .Include(a => a.Session)
            .OrderBy(a => a.Student.User.LastName)
            .ThenBy(a => a.Student.User.FirstName)
            .ThenBy(a => a.Id)
            .ToListAsync(cancellationToken);

        var dtos = records.Select(a => MapToDto(a)).ToList();

        _logger.LogInformation("Retrieved {Count} attendance record(s) for session '{SessionId}'.", dtos.Count, sessionId);

        return dtos;
    }

    public async Task<AttendanceDto> CreateAsync(CreateAttendanceDto dto, Guid? currentUserId = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var organizationId = _currentOrganizationService.OrganizationId;
        if (!organizationId.HasValue || organizationId.Value == Guid.Empty)
        {
            _logger.LogWarning("Cannot create attendance record: Organization ID could not be determined from current context.");
            throw new BadRequestException("Organization context is required.");
        }

        _logger.LogInformation("Creating attendance record for student '{StudentId}' and session '{SessionId}' in org '{OrganizationId}'.",
            dto.StudentId, dto.SessionId, organizationId.Value);

        await _attendanceValidator.ValidateCreateAsync(dto, organizationId.Value, currentUserId, cancellationToken);

        var record = AttendanceRecord.Create(
            organizationId.Value,
            dto.SessionId,
            dto.StudentId,
            dto.Status,
            currentUserId,
            dto.Notes);

        _dbContext.AttendanceRecords.Add(record);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Attendance record '{AttendanceId}' created successfully.", record.Id);

        var created = await _dbContext.AttendanceRecords
            .AsNoTracking()
            .Include(a => a.Student)
                .ThenInclude(s => s.User)
            .Include(a => a.MarkedByUser)
            .Include(a => a.Session)
            .FirstAsync(a => a.Id == record.Id, cancellationToken);

        return MapToDto(created);
    }

    public async Task<AttendanceDto> UpdateAsync(Guid id, UpdateAttendanceDto dto, Guid? currentUserId = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var organizationId = _currentOrganizationService.OrganizationId;
        if (!organizationId.HasValue || organizationId.Value == Guid.Empty)
        {
            _logger.LogWarning("Cannot update attendance record: Organization ID could not be determined from current context.");
            throw new BadRequestException("Organization context is required.");
        }

        _logger.LogInformation("Updating attendance record '{Id}' in org '{OrganizationId}'.", id, organizationId.Value);

        await _attendanceValidator.ValidateUpdateAsync(id, dto, organizationId.Value, currentUserId, cancellationToken);

        var record = await _dbContext.AttendanceRecords
            .Include(a => a.Student)
                .ThenInclude(s => s.User)
            .Include(a => a.MarkedByUser)
            .Include(a => a.Session)
            .FirstOrDefaultAsync(a => a.Id == id && a.OrganizationId == organizationId.Value, cancellationToken);

        if (record == null)
        {
            _logger.LogWarning("Attendance record '{Id}' was not found for update.", id);
            throw new NotFoundException("AttendanceRecord", id);
        }

        record.UpdateDetails(dto.Status, dto.Notes, currentUserId);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Attendance record '{Id}' updated successfully.", id);

        return MapToDto(record);
    }

    public async Task<AttendanceDto> UpdateStatusAsync(Guid id, UpdateAttendanceStatusDto dto, Guid? currentUserId = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var organizationId = _currentOrganizationService.OrganizationId;
        if (!organizationId.HasValue || organizationId.Value == Guid.Empty)
        {
            _logger.LogWarning("Cannot update attendance status: Organization ID could not be determined from current context.");
            throw new BadRequestException("Organization context is required.");
        }

        _logger.LogInformation("Updating status of attendance record '{Id}' to '{Status}' in org '{OrganizationId}'.",
            id, dto.Status, organizationId.Value);

        await _attendanceValidator.ValidateUpdateStatusAsync(id, dto.Status, organizationId.Value, currentUserId, cancellationToken);

        var record = await _dbContext.AttendanceRecords
            .Include(a => a.Student)
                .ThenInclude(s => s.User)
            .Include(a => a.MarkedByUser)
            .Include(a => a.Session)
            .FirstOrDefaultAsync(a => a.Id == id && a.OrganizationId == organizationId.Value, cancellationToken);

        if (record == null)
        {
            _logger.LogWarning("Attendance record '{Id}' was not found for status update.", id);
            throw new NotFoundException("AttendanceRecord", id);
        }

        record.UpdateStatus(dto.Status, currentUserId);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Attendance record '{Id}' status updated successfully.", id);

        return MapToDto(record);
    }

    public async Task DeleteAsync(Guid id, Guid? currentUserId = null, CancellationToken cancellationToken = default)
    {
        var organizationId = _currentOrganizationService.OrganizationId;
        if (!organizationId.HasValue || organizationId.Value == Guid.Empty)
        {
            _logger.LogWarning("Cannot delete attendance record: Organization ID could not be determined from current context.");
            throw new BadRequestException("Organization context is required.");
        }

        _logger.LogInformation("Deleting attendance record '{Id}' from org '{OrganizationId}'.", id, organizationId.Value);

        await _attendanceValidator.ValidateDeleteAsync(id, organizationId.Value, currentUserId, cancellationToken);

        var record = await _dbContext.AttendanceRecords
            .FirstOrDefaultAsync(a => a.Id == id && a.OrganizationId == organizationId.Value, cancellationToken);

        if (record == null)
        {
            _logger.LogWarning("Attendance record '{Id}' was not found for deletion.", id);
            throw new NotFoundException("AttendanceRecord", id);
        }

        _dbContext.AttendanceRecords.Remove(record);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Attendance record '{Id}' deleted successfully.", id);
    }

    private static AttendanceDto MapToDto(AttendanceRecord record)
    {
        var student = record.Student;
        var studentUser = student?.User;
        var studentName = studentUser != null
            ? $"{studentUser.FirstName} {studentUser.LastName}".Trim()
            : string.Empty;

        var markedByUser = record.MarkedByUser;
        var markedByName = markedByUser != null
            ? $"{markedByUser.FirstName} {markedByUser.LastName}".Trim()
            : null;

        return new AttendanceDto
        {
            Id = record.Id,
            OrganizationId = record.OrganizationId,
            SessionId = record.SessionId,
            StudentId = record.StudentId,
            StudentName = studentName,
            StudentCode = student?.StudentCode ?? string.Empty,
            MarkedByUserId = record.MarkedByUserId,
            MarkedByName = markedByName,
            Status = record.Status,
            StatusName = AttendanceConstants.Statuses.GetName(record.Status),
            MarkedAt = record.MarkedAt,
            Notes = record.Notes
        };
    }
}
