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

    public Task<PagedResult<AttendanceDto>> GetPagedAsync(AttendanceQueryParameters parameters, CancellationToken cancellationToken = default) =>
        GetPagedAsync(parameters, null, cancellationToken);

    public async Task<PagedResult<AttendanceDto>> GetPagedAsync(AttendanceQueryParameters parameters, Guid? currentUserId, CancellationToken cancellationToken = default)
    {
        parameters ??= new AttendanceQueryParameters();

        _logger.LogInformation("Retrieving paged attendance records: Page {PageNumber}, Size {PageSize}, Sort {SortBy} (Desc: {Desc}).",
            parameters.PageNumber, parameters.PageSize, parameters.SortBy, parameters.SortDescending);

        _attendanceValidator.ValidateQuery(parameters);

        var organizationId = _currentOrganizationService.OrganizationId;

        var query = _dbContext.AttendanceRecords
            .AsNoTracking()
            .Include(a => a.Student)
                .ThenInclude(s => s.User)
            .Include(a => a.MarkedByUser)
            .Include(a => a.Session)
                .ThenInclude(s => s.CourseGroup)
            .AsQueryable();

        if (organizationId.HasValue && organizationId.Value != Guid.Empty)
        {
            query = query.Where(a => a.OrganizationId == organizationId.Value);

            // Apply caller-based authorization/filtering if current user context is provided
            if (currentUserId.HasValue)
            {
                var caller = await _attendanceValidator.ResolveCallerPermissionsAsync(currentUserId.Value, organizationId.Value, cancellationToken);

                if (!caller.IsOrgStaff)
                {
                    if (caller.Student != null)
                    {
                        if (parameters.StudentId.HasValue && parameters.StudentId.Value != caller.Student.Id)
                        {
                            _logger.LogWarning("Student '{StudentId}' attempted to query attendance of student '{TargetStudentId}'.",
                                caller.Student.Id, parameters.StudentId.Value);
                            throw new ForbiddenAccessException("Students can only view their own attendance records.");
                        }

                        query = query.Where(a => a.StudentId == caller.Student.Id);
                    }
                    else if (caller.Instructor != null)
                    {
                        query = query.Where(a => a.Session.InstructorId == caller.Instructor.Id ||
                                                 (a.Session.CourseGroup != null && a.Session.CourseGroup.InstructorId == caller.Instructor.Id));
                    }
                    else
                    {
                        throw new ForbiddenAccessException("You are not authorized to view attendance records.");
                    }
                }
            }
        }

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

    public Task<AttendanceDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        GetByIdAsync(id, null, cancellationToken);

    public async Task<AttendanceDto> GetByIdAsync(Guid id, Guid? currentUserId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Retrieving attendance record with ID '{Id}'.", id);

        var organizationId = _currentOrganizationService.OrganizationId;
        if (organizationId.HasValue && organizationId.Value != Guid.Empty)
        {
            await _attendanceValidator.ValidateGetByIdAsync(id, organizationId.Value, currentUserId, cancellationToken);
        }

        var query = _dbContext.AttendanceRecords
            .AsNoTracking()
            .Include(a => a.Student)
                .ThenInclude(s => s.User)
            .Include(a => a.MarkedByUser)
            .Include(a => a.Session)
                .ThenInclude(s => s.CourseGroup)
            .AsQueryable();

        if (organizationId.HasValue && organizationId.Value != Guid.Empty)
        {
            query = query.Where(a => a.OrganizationId == organizationId.Value);
        }

        var record = await query.FirstOrDefaultAsync(a => a.Id == id, cancellationToken);

        if (record == null)
        {
            _logger.LogWarning("Attendance record '{Id}' was not found.", id);
            throw new NotFoundException("AttendanceRecord", id);
        }

        return MapToDto(record);
    }

    public Task<IReadOnlyList<AttendanceDto>> GetBySessionIdAsync(Guid sessionId, CancellationToken cancellationToken = default) =>
        GetBySessionIdAsync(sessionId, null, cancellationToken);

    public async Task<IReadOnlyList<AttendanceDto>> GetBySessionIdAsync(Guid sessionId, Guid? currentUserId, CancellationToken cancellationToken = default)
    {
        var organizationId = _currentOrganizationService.OrganizationId;
        if (!organizationId.HasValue || organizationId.Value == Guid.Empty)
        {
            _logger.LogWarning("Cannot retrieve attendance records: Organization ID could not be determined from current context.");
            throw new BadRequestException("Organization context is required.");
        }

        await _attendanceValidator.ValidateGetBySessionIdAsync(sessionId, organizationId.Value, currentUserId, cancellationToken);

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
                .ThenInclude(s => s.CourseGroup)
            .OrderBy(a => a.Student.User.LastName)
            .ThenBy(a => a.Student.User.FirstName)
            .ThenBy(a => a.Id)
            .ToListAsync(cancellationToken);

        var dtos = records.Select(a => MapToDto(a)).ToList();

        _logger.LogInformation("Retrieved {Count} attendance record(s) for session '{SessionId}'.", dtos.Count, sessionId);

        return dtos;
    }

    public async Task<AttendancePercentageDto> GetPercentageAsync(
        Guid studentId,
        Guid? courseGroupId = null,
        Guid? currentUserId = null,
        CancellationToken cancellationToken = default)
    {
        var organizationId = _currentOrganizationService.OrganizationId;
        if (!organizationId.HasValue || organizationId.Value == Guid.Empty)
        {
            _logger.LogWarning("Cannot calculate attendance percentage: Organization ID could not be determined from current context.");
            throw new BadRequestException("Organization context is required.");
        }

        _logger.LogInformation("Calculating attendance percentage for student '{StudentId}' (Group: {CourseGroupId}) in org '{OrganizationId}'.",
            studentId, courseGroupId, organizationId.Value);

        await _attendanceValidator.ValidateGetPercentageAsync(studentId, courseGroupId, organizationId.Value, currentUserId, cancellationToken);

        var student = await _dbContext.Students
            .AsNoTracking()
            .Include(s => s.User)
            .FirstOrDefaultAsync(s => (s.Id == studentId || s.UserId == studentId) && s.OrganizationId == organizationId.Value, cancellationToken);

        if (student == null)
        {
            throw new NotFoundException("Student", studentId);
        }

        CourseGroup? courseGroup = null;
        if (courseGroupId.HasValue)
        {
            courseGroup = await _dbContext.CourseGroups
                .AsNoTracking()
                .FirstOrDefaultAsync(cg => cg.Id == courseGroupId.Value && cg.OrganizationId == organizationId.Value, cancellationToken);
        }

        var query = _dbContext.AttendanceRecords
            .AsNoTracking()
            .Where(a => a.StudentId == student.Id && a.OrganizationId == organizationId.Value);

        if (courseGroupId.HasValue)
        {
            query = query.Where(a => a.Session.CourseGroupId == courseGroupId.Value);
        }

        var records = await query.ToListAsync(cancellationToken);

        var totalRecords = records.Count;
        var presentCount = records.Count(r => r.Status == AttendanceConstants.Statuses.Present);
        var lateCount = records.Count(r => r.Status == AttendanceConstants.Statuses.Late);
        var absentCount = records.Count(r => r.Status == AttendanceConstants.Statuses.Absent);
        var excusedCount = records.Count(r => r.Status == AttendanceConstants.Statuses.Excused);
        var attendedCount = presentCount + lateCount;

        var percentage = AttendanceConstants.CalculatePercentage(attendedCount, totalRecords);

        var studentName = student.User != null
            ? $"{student.User.FirstName} {student.User.LastName}".Trim()
            : string.Empty;

        return new AttendancePercentageDto
        {
            StudentId = student.Id,
            StudentName = studentName,
            StudentCode = student.StudentCode,
            CourseGroupId = courseGroup?.Id,
            CourseGroupName = courseGroup?.Name,
            TotalRecords = totalRecords,
            AttendedCount = attendedCount,
            PresentCount = presentCount,
            LateCount = lateCount,
            AbsentCount = absentCount,
            ExcusedCount = excusedCount,
            AttendancePercentage = percentage
        };
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
                .ThenInclude(s => s.CourseGroup)
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
                .ThenInclude(s => s.CourseGroup)
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
                .ThenInclude(s => s.CourseGroup)
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

    public async Task<IReadOnlyList<AttendanceDto>> SaveBatchAsync(
        Guid sessionId,
        BatchAttendanceRequestDto dto,
        Guid? currentUserId = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var organizationId = _currentOrganizationService.OrganizationId;
        if (!organizationId.HasValue || organizationId.Value == Guid.Empty)
        {
            _logger.LogWarning("Cannot save batch attendance: Organization ID could not be determined from current context.");
            throw new BadRequestException("Organization context is required.");
        }

        _logger.LogInformation("Saving batch attendance for session '{SessionId}' ({Count} records) in org '{OrganizationId}'.",
            sessionId, dto.Records?.Count ?? 0, organizationId.Value);

        await _attendanceValidator.ValidateBatchAsync(sessionId, dto, organizationId.Value, currentUserId, cancellationToken);

        if (dto.Records == null || dto.Records.Count == 0)
        {
            throw new ValidationException(nameof(dto.Records), "Batch must contain at least one attendance record.");
        }

        var transaction = await BeginTransactionAsync(cancellationToken);
        await using (transaction)
        {
            var isCommitted = false;
            var studentIds = dto.Records.Select(r => r.StudentId).ToList();

            try
            {
                var existingRecords = await _dbContext.AttendanceRecords
                    .Where(a => a.SessionId == sessionId && a.OrganizationId == organizationId.Value && studentIds.Contains(a.StudentId))
                    .ToListAsync(cancellationToken);

                var existingMap = existingRecords.ToDictionary(a => a.StudentId);

                foreach (var item in dto.Records)
                {
                    if (existingMap.TryGetValue(item.StudentId, out var existing))
                    {
                        existing.UpdateDetails(item.Status, item.Notes, currentUserId);
                        _dbContext.AttendanceRecords.Update(existing);
                    }
                    else
                    {
                        var newRecord = AttendanceRecord.Create(
                            organizationId.Value,
                            sessionId,
                            item.StudentId,
                            item.Status,
                            currentUserId,
                            item.Notes);

                        _dbContext.AttendanceRecords.Add(newRecord);
                    }
                }

                await _dbContext.SaveChangesAsync(cancellationToken);

                if (transaction != null)
                {
                    await transaction.CommitAsync(cancellationToken);
                    isCommitted = true;
                }
            }
            catch (Exception ex)
            {
                if (transaction != null && !isCommitted)
                {
                    try
                    {
                        await transaction.RollbackAsync(cancellationToken);
                    }
                    catch (Exception rollbackEx)
                    {
                        _logger.LogError(rollbackEx, "Failed to roll back transaction for batch attendance on session '{SessionId}' during exception: {OriginalError}.", sessionId, ex.Message);
                    }
                }
                throw;
            }

            _logger.LogInformation("Successfully persisted batch attendance for session '{SessionId}' ({Count} records).",
                sessionId, dto.Records.Count);

            try
            {
                return await MaterializeBatchResponseAsync(sessionId, organizationId.Value, studentIds, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Batch attendance records were committed successfully for session '{SessionId}', but an error occurred while materializing the response.", sessionId);
                throw;
            }
        }
    }

    protected virtual async Task<Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction?> BeginTransactionAsync(CancellationToken cancellationToken)
    {
        if (_dbContext.Database.ProviderName?.Contains("InMemory", StringComparison.OrdinalIgnoreCase) == true)
        {
            return null;
        }

        try
        {
            return await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Database provider '{Provider}' does not support explicit transactions; proceeding without explicit transaction.",
                _dbContext.Database.ProviderName);
            return null;
        }
    }

    protected virtual async Task<IReadOnlyList<AttendanceDto>> MaterializeBatchResponseAsync(
        Guid sessionId,
        Guid organizationId,
        List<Guid> studentIds,
        CancellationToken cancellationToken)
    {
        var savedRecords = await _dbContext.AttendanceRecords
            .AsNoTracking()
            .Where(a => a.SessionId == sessionId && a.OrganizationId == organizationId && studentIds.Contains(a.StudentId))
            .Include(a => a.Student)
                .ThenInclude(s => s.User)
            .Include(a => a.MarkedByUser)
            .Include(a => a.Session)
                .ThenInclude(s => s.CourseGroup)
            .OrderBy(a => a.Student.User.LastName)
            .ThenBy(a => a.Student.User.FirstName)
            .ToListAsync(cancellationToken);

        return savedRecords.Select(MapToDto).ToList();
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

        var session = record.Session;
        var courseGroup = session?.CourseGroup;

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
            Notes = record.Notes,
            SessionTitle = session?.Room ?? courseGroup?.Name ?? "Session",
            CourseGroupName = courseGroup?.Name,
            SessionDate = session?.StartTime
        };
    }
}
