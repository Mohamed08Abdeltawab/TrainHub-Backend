using System.Collections.Concurrent;
using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using TrainHub.Application.Common.Exceptions;
using TrainHub.Application.Common.Models;
using TrainHub.Application.Features.Enrollments.DTOs;
using TrainHub.Application.Interfaces;
using TrainHub.Domain.Constants;
using TrainHub.Domain.Models;

namespace TrainHub.Application.Services;

/// <summary>
/// Service implementation for managing Enrollment entities and operations.
/// </summary>
public class EnrollmentService : IEnrollmentService
{
    private static readonly ConcurrentDictionary<Guid, SemaphoreSlim> _groupLocks = new();
    private readonly ITrainHubDbContext _dbContext;
    private readonly IEnrollmentValidator _enrollmentValidator;
    private readonly ICurrentOrganizationService _currentOrganizationService;
    private readonly ILogger<EnrollmentService> _logger;

    public EnrollmentService(
        ITrainHubDbContext dbContext,
        IEnrollmentValidator enrollmentValidator,
        ICurrentOrganizationService currentOrganizationService,
        ILogger<EnrollmentService> logger)
    {
        _dbContext = dbContext;
        _enrollmentValidator = enrollmentValidator;
        _currentOrganizationService = currentOrganizationService;
        _logger = logger;
    }

    public async Task<PagedResult<EnrollmentDto>> GetPagedAsync(EnrollmentQueryParameters parameters, CancellationToken cancellationToken = default)
    {
        parameters ??= new EnrollmentQueryParameters();

        _logger.LogInformation("Retrieving paged enrollments: Page {PageNumber}, Size {PageSize}, Sort {SortBy} (Desc: {Desc}).",
            parameters.PageNumber, parameters.PageSize, parameters.SortBy, parameters.SortDescending);

        _enrollmentValidator.ValidateQuery(parameters);

        var query = _dbContext.Enrollments
            .AsNoTracking()
            .Include(e => e.CourseGroup)
                .ThenInclude(cg => cg.Course)
            .Include(e => e.Student)
                .ThenInclude(s => s.User)
            .AsQueryable();

        if (parameters.CourseGroupId.HasValue)
        {
            query = query.Where(e => e.CourseGroupId == parameters.CourseGroupId.Value);
        }

        if (parameters.StudentId.HasValue)
        {
            query = query.Where(e => e.StudentId == parameters.StudentId.Value);
        }

        if (parameters.CourseId.HasValue)
        {
            query = query.Where(e => e.CourseGroup.CourseId == parameters.CourseId.Value);
        }

        if (parameters.Status.HasValue)
        {
            query = query.Where(e => e.Status == parameters.Status.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var isDescending = parameters.SortDescending;
        query = (parameters.SortBy?.Trim().ToLowerInvariant()) switch
        {
            "enrollmentdate" => isDescending ? query.OrderByDescending(e => e.EnrollmentDate).ThenBy(e => e.Id) : query.OrderBy(e => e.EnrollmentDate).ThenBy(e => e.Id),
            "paidamount" => isDescending ? query.OrderByDescending(e => e.PaidAmount).ThenBy(e => e.Id) : query.OrderBy(e => e.PaidAmount).ThenBy(e => e.Id),
            "status" => isDescending ? query.OrderByDescending(e => e.Status).ThenBy(e => e.Id) : query.OrderBy(e => e.Status).ThenBy(e => e.Id),
            "updatedat" => isDescending ? query.OrderByDescending(e => e.UpdatedAt).ThenBy(e => e.Id) : query.OrderBy(e => e.UpdatedAt).ThenBy(e => e.Id),
            _ => isDescending ? query.OrderByDescending(e => e.CreatedAt).ThenBy(e => e.Id) : query.OrderBy(e => e.CreatedAt).ThenBy(e => e.Id)
        };

        var skip = (parameters.PageNumber - 1) * parameters.PageSize;
        var items = await query
            .Skip(skip)
            .Take(parameters.PageSize)
            .ToListAsync(cancellationToken);

        var dtos = items.Select(e => MapToDto(e)).ToList();

        return new PagedResult<EnrollmentDto>(dtos, totalCount, parameters.PageNumber, parameters.PageSize);
    }

    public async Task<EnrollmentDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Retrieving enrollment with ID '{EnrollmentId}'.", id);

        var enrollment = await _dbContext.Enrollments
            .AsNoTracking()
            .Include(e => e.CourseGroup)
                .ThenInclude(cg => cg.Course)
            .Include(e => e.Student)
                .ThenInclude(s => s.User)
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken);

        if (enrollment == null)
        {
            _logger.LogWarning("Enrollment '{EnrollmentId}' was not found.", id);
            throw new NotFoundException("Enrollment", id);
        }

        return MapToDto(enrollment);
    }

    public async Task<EnrollmentDto> CreateAsync(CreateEnrollmentDto dto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var organizationId = _currentOrganizationService.OrganizationId;
        if (!organizationId.HasValue || organizationId.Value == Guid.Empty)
        {
            _logger.LogWarning("Cannot create enrollment: Organization ID could not be determined from current context.");
            throw new BadRequestException("Organization context is required.");
        }

        _logger.LogInformation("Creating enrollment for student '{StudentId}' in group '{CourseGroupId}' for org '{OrganizationId}'.",
            dto.StudentId, dto.CourseGroupId, organizationId.Value);

        await _enrollmentValidator.ValidateCreateAsync(dto, organizationId.Value, cancellationToken);

        var groupLock = _groupLocks.GetOrAdd(dto.CourseGroupId, _ => new SemaphoreSlim(1, 1));
        await groupLock.WaitAsync(cancellationToken);

        var transaction = await BeginTransactionAsync(cancellationToken);
        await using (transaction)
        {
            var isCommitted = false;
            Guid enrollmentId;

            try
            {
                // ── Re-verify active group capacity & uniqueness within synchronization scope ──
                var courseGroup = await _dbContext.GetCourseGroupWithLockAsync(dto.CourseGroupId, organizationId.Value, cancellationToken);

                if (courseGroup == null)
                {
                    throw new NotFoundException("CourseGroup", dto.CourseGroupId);
                }

                var activeEnrolledCount = await _dbContext.Enrollments
                    .IgnoreQueryFilters()
                    .CountAsync(e => e.CourseGroupId == dto.CourseGroupId && e.Status != EnrollmentConstants.Statuses.Cancelled, cancellationToken);

                if (activeEnrolledCount >= courseGroup.Capacity)
                {
                    _logger.LogWarning("Concurrent enrollment rejected: Course group '{CourseGroupId}' reached capacity ({Capacity}).",
                        dto.CourseGroupId, courseGroup.Capacity);
                    throw new ConflictException($"Course group '{courseGroup.Name}' has reached its maximum capacity of {courseGroup.Capacity}.");
                }

                var alreadyEnrolled = await _dbContext.Enrollments
                    .IgnoreQueryFilters()
                    .AnyAsync(e => e.CourseGroupId == dto.CourseGroupId && e.StudentId == dto.StudentId, cancellationToken);

                if (alreadyEnrolled)
                {
                    _logger.LogWarning("Concurrent enrollment rejected: Student '{StudentId}' is already enrolled in group '{CourseGroupId}'.",
                        dto.StudentId, dto.CourseGroupId);
                    throw new ConflictException("Student is already enrolled in this course group.");
                }

                var enrollment = Enrollment.Create(
                    organizationId.Value,
                    dto.CourseGroupId,
                    dto.StudentId,
                    dto.PaidAmount ?? 0m,
                    dto.Status,
                    dto.EnrollmentDate);

                _dbContext.Enrollments.Add(enrollment);

                await _dbContext.SaveChangesAsync(cancellationToken);

                if (transaction != null)
                {
                    await transaction.CommitAsync(cancellationToken);
                    isCommitted = true;
                }

                enrollmentId = enrollment.Id;
            }
            catch (DbUpdateException ex)
            {
                if (transaction != null && !isCommitted)
                {
                    try
                    {
                        await transaction.RollbackAsync(cancellationToken);
                    }
                    catch (Exception rollbackEx)
                    {
                        _logger.LogError(rollbackEx, "Failed to roll back transaction for enrollment during DB update exception: {OriginalError}.", ex.Message);
                    }
                }
                _logger.LogWarning(ex, "Failed to create enrollment due to database conflict (duplicate enrollment constraint).");
                throw new ConflictException("Student is already enrolled in this course group.");
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
                        _logger.LogError(rollbackEx, "Failed to roll back transaction for enrollment during exception: {OriginalError}.", ex.Message);
                    }
                }
                throw;
            }
            finally
            {
                groupLock.Release();
            }

            _logger.LogInformation("Enrollment '{EnrollmentId}' created successfully.", enrollmentId);

            try
            {
                return await MaterializeEnrollmentResponseAsync(enrollmentId, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Enrollment was committed successfully with ID '{EnrollmentId}', but an error occurred while materializing the response.", enrollmentId);
                throw;
            }
        }
    }

    public async Task<EnrollmentDto> UpdateAsync(Guid id, UpdateEnrollmentDto dto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var organizationId = _currentOrganizationService.OrganizationId;
        if (!organizationId.HasValue || organizationId.Value == Guid.Empty)
        {
            _logger.LogWarning("Cannot update enrollment: Organization ID could not be determined from current context.");
            throw new BadRequestException("Organization context is required.");
        }

        _logger.LogInformation("Updating enrollment '{EnrollmentId}' in organization '{OrganizationId}'.", id, organizationId.Value);

        await _enrollmentValidator.ValidateUpdateAsync(id, dto, organizationId.Value, cancellationToken);

        var enrollment = await _dbContext.Enrollments
            .Include(e => e.CourseGroup)
                .ThenInclude(cg => cg.Course)
            .Include(e => e.Student)
                .ThenInclude(s => s.User)
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken);

        if (enrollment == null)
        {
            _logger.LogWarning("Enrollment '{EnrollmentId}' was not found.", id);
            throw new NotFoundException("Enrollment", id);
        }

        var isReactivating = enrollment.Status == EnrollmentConstants.Statuses.Cancelled &&
                             dto.Status.HasValue &&
                             dto.Status.Value != EnrollmentConstants.Statuses.Cancelled;

        if (isReactivating)
        {
            var groupLock = _groupLocks.GetOrAdd(enrollment.CourseGroupId, _ => new SemaphoreSlim(1, 1));
            await groupLock.WaitAsync(cancellationToken);

            var transaction = await BeginTransactionAsync(cancellationToken);
            await using (transaction)
            {
                var isCommitted = false;
                try
                {
                    var courseGroup = await _dbContext.GetCourseGroupWithLockAsync(enrollment.CourseGroupId, organizationId.Value, cancellationToken);
                    if (courseGroup == null)
                    {
                        throw new NotFoundException("CourseGroup", enrollment.CourseGroupId);
                    }

                    var activeEnrolledCount = await _dbContext.Enrollments
                        .IgnoreQueryFilters()
                        .CountAsync(e => e.CourseGroupId == enrollment.CourseGroupId && e.Status != EnrollmentConstants.Statuses.Cancelled && e.Id != enrollment.Id, cancellationToken);

                    if (activeEnrolledCount >= courseGroup.Capacity)
                    {
                        _logger.LogWarning("Concurrent reactivation rejected: Course group '{CourseGroupId}' reached capacity ({Capacity}).",
                            enrollment.CourseGroupId, courseGroup.Capacity);
                        throw new ConflictException("Cannot reactivate enrollment because the course group has reached its maximum capacity.");
                    }

                    enrollment.UpdateDetails(dto.PaidAmount, dto.Status);
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
                            _logger.LogError(rollbackEx, "Failed to roll back transaction for enrollment update: {OriginalError}.", ex.Message);
                        }
                    }
                    throw;
                }
                finally
                {
                    groupLock.Release();
                }
            }
        }
        else
        {
            enrollment.UpdateDetails(dto.PaidAmount, dto.Status);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        _logger.LogInformation("Enrollment '{EnrollmentId}' updated successfully.", id);

        return MapToDto(enrollment);
    }

    public async Task<EnrollmentDto> UpdateStatusAsync(Guid id, UpdateEnrollmentStatusDto dto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        _logger.LogInformation("Updating status of enrollment '{EnrollmentId}' to '{Status}'.", id, dto.Status);

        await _enrollmentValidator.ValidateUpdateStatusAsync(id, dto.Status, cancellationToken);

        var enrollment = await _dbContext.Enrollments
            .Include(e => e.CourseGroup)
                .ThenInclude(cg => cg.Course)
            .Include(e => e.Student)
                .ThenInclude(s => s.User)
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken);

        if (enrollment == null)
        {
            _logger.LogWarning("Enrollment '{EnrollmentId}' was not found.", id);
            throw new NotFoundException("Enrollment", id);
        }

        var isReactivating = enrollment.Status == EnrollmentConstants.Statuses.Cancelled &&
                             dto.Status != EnrollmentConstants.Statuses.Cancelled;

        if (isReactivating)
        {
            var groupLock = _groupLocks.GetOrAdd(enrollment.CourseGroupId, _ => new SemaphoreSlim(1, 1));
            await groupLock.WaitAsync(cancellationToken);

            var transaction = await BeginTransactionAsync(cancellationToken);
            await using (transaction)
            {
                var isCommitted = false;
                try
                {
                    var courseGroup = await _dbContext.GetCourseGroupWithLockAsync(enrollment.CourseGroupId, enrollment.OrganizationId, cancellationToken);
                    if (courseGroup == null)
                    {
                        throw new NotFoundException("CourseGroup", enrollment.CourseGroupId);
                    }

                    var activeEnrolledCount = await _dbContext.Enrollments
                        .IgnoreQueryFilters()
                        .CountAsync(e => e.CourseGroupId == enrollment.CourseGroupId && e.Status != EnrollmentConstants.Statuses.Cancelled && e.Id != enrollment.Id, cancellationToken);

                    if (activeEnrolledCount >= courseGroup.Capacity)
                    {
                        _logger.LogWarning("Concurrent reactivation rejected: Course group '{CourseGroupId}' reached capacity ({Capacity}).",
                            enrollment.CourseGroupId, courseGroup.Capacity);
                        throw new ConflictException("Cannot activate enrollment because the course group has reached its maximum capacity.");
                    }

                    enrollment.UpdateStatus(dto.Status);
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
                            _logger.LogError(rollbackEx, "Failed to roll back transaction for enrollment status update: {OriginalError}.", ex.Message);
                        }
                    }
                    throw;
                }
                finally
                {
                    groupLock.Release();
                }
            }
        }
        else
        {
            enrollment.UpdateStatus(dto.Status);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        _logger.LogInformation("Status of enrollment '{EnrollmentId}' updated to '{Status}'.", id, dto.Status);

        return MapToDto(enrollment);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Deleting enrollment '{EnrollmentId}'.", id);

        await _enrollmentValidator.ValidateDeleteAsync(id, cancellationToken);

        var enrollment = await _dbContext.Enrollments
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken);

        if (enrollment == null)
        {
            _logger.LogWarning("Enrollment '{EnrollmentId}' was not found for deletion.", id);
            throw new NotFoundException("Enrollment", id);
        }

        _dbContext.Enrollments.Remove(enrollment);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Enrollment '{EnrollmentId}' deleted successfully.", id);
    }

    protected virtual async Task<IDbContextTransaction?> BeginTransactionAsync(CancellationToken cancellationToken)
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

    protected virtual async Task<EnrollmentDto> MaterializeEnrollmentResponseAsync(Guid enrollmentId, CancellationToken cancellationToken)
    {
        var created = await _dbContext.Enrollments
            .AsNoTracking()
            .Include(e => e.CourseGroup)
                .ThenInclude(cg => cg.Course)
            .Include(e => e.Student)
                .ThenInclude(s => s.User)
            .FirstAsync(e => e.Id == enrollmentId, cancellationToken);

        return MapToDto(created);
    }

    private static EnrollmentDto MapToDto(Enrollment enrollment, CourseGroup? group = null, Student? student = null)
    {
        var resolvedGroup = group ?? enrollment.CourseGroup;
        var resolvedStudent = student ?? enrollment.Student;
        var studentName = resolvedStudent?.User != null
            ? $"{resolvedStudent.User.FirstName} {resolvedStudent.User.LastName}".Trim()
            : string.Empty;

        return new EnrollmentDto
        {
            Id = enrollment.Id,
            OrganizationId = enrollment.OrganizationId,
            CourseGroupId = enrollment.CourseGroupId,
            CourseGroupName = resolvedGroup?.Name ?? string.Empty,
            CourseId = resolvedGroup?.CourseId ?? Guid.Empty,
            CourseName = resolvedGroup?.Course?.Name ?? string.Empty,
            StudentId = enrollment.StudentId,
            StudentCode = resolvedStudent?.StudentCode ?? string.Empty,
            StudentName = studentName,
            StudentEmail = resolvedStudent?.User?.Email,
            EnrollmentDate = enrollment.EnrollmentDate,
            PaidAmount = enrollment.PaidAmount,
            Status = enrollment.Status,
            StatusName = EnrollmentConstants.Statuses.GetName(enrollment.Status),
            CreatedAt = enrollment.CreatedAt,
            UpdatedAt = enrollment.UpdatedAt
        };
    }
}
