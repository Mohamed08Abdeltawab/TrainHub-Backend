using Microsoft.EntityFrameworkCore;
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

        var enrollment = Enrollment.Create(
            organizationId.Value,
            dto.CourseGroupId,
            dto.StudentId,
            dto.PaidAmount ?? 0m,
            dto.Status,
            dto.EnrollmentDate);

        _dbContext.Enrollments.Add(enrollment);
        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            _logger.LogWarning(ex, "Failed to create enrollment due to database conflict (duplicate enrollment constraint).");
            throw new ConflictException("Student is already enrolled in this course group.");
        }

        _logger.LogInformation("Enrollment '{EnrollmentId}' created successfully.", enrollment.Id);

        var created = await _dbContext.Enrollments
            .AsNoTracking()
            .Include(e => e.CourseGroup)
                .ThenInclude(cg => cg.Course)
            .Include(e => e.Student)
                .ThenInclude(s => s.User)
            .FirstAsync(e => e.Id == enrollment.Id, cancellationToken);

        return MapToDto(created);
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

        enrollment.UpdateDetails(dto.PaidAmount, dto.Status);
        await _dbContext.SaveChangesAsync(cancellationToken);

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

        enrollment.UpdateStatus(dto.Status);
        await _dbContext.SaveChangesAsync(cancellationToken);

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
