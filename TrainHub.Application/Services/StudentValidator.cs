using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TrainHub.Application.Common.Exceptions;
using TrainHub.Application.Features.Students.DTOs;
using TrainHub.Application.Interfaces;
using TrainHub.Domain.Constants;

namespace TrainHub.Application.Services;

/// <summary>
/// Validates business rules, unique constraints, and relational integrity for Students.
/// </summary>
public class StudentValidator : IStudentValidator
{
    private readonly ITrainHubDbContext _dbContext;
    private readonly ILogger<StudentValidator> _logger;

    public StudentValidator(ITrainHubDbContext dbContext, ILogger<StudentValidator> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public void ValidateQuery(StudentQueryParameters query)
    {
        ArgumentNullException.ThrowIfNull(query);

        var errors = new Dictionary<string, string[]>();

        if (query.PageNumber < 1)
        {
            errors[nameof(query.PageNumber)] = ["Page number must be at least 1."];
        }

        if (query.PageSize < 1 || query.PageSize > StudentConstants.MaxPageSize)
        {
            errors[nameof(query.PageSize)] = [$"Page size must be between 1 and {StudentConstants.MaxPageSize}."];
        }

        if (!string.IsNullOrWhiteSpace(query.SearchTerm) && query.SearchTerm.Trim().Length > StudentConstants.SearchTermMaxLength)
        {
            errors[nameof(query.SearchTerm)] = [$"Search term cannot exceed {StudentConstants.SearchTermMaxLength} characters."];
        }

        if (query.DateOfBirthFrom.HasValue && query.DateOfBirthTo.HasValue && query.DateOfBirthFrom.Value > query.DateOfBirthTo.Value)
        {
            errors[nameof(query.DateOfBirthFrom)] = ["Date of birth 'from' date cannot be after 'to' date."];
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        if (query.DateOfBirthTo.HasValue && query.DateOfBirthTo.Value > today)
        {
            errors[nameof(query.DateOfBirthTo)] = ["Date of birth filter cannot be in the future."];
        }

        if (!string.IsNullOrWhiteSpace(query.SortBy) && !StudentConstants.IsValidSortField(query.SortBy))
        {
            errors[nameof(query.SortBy)] = [$"Invalid sort field '{query.SortBy}'. Allowed values: {string.Join(", ", StudentConstants.AllowedSortFields)}."];
        }

        if (errors.Count > 0)
        {
            _logger.LogWarning("Validation failed for student query parameters: {ErrorCount} errors.", errors.Count);
            throw new ValidationException(errors);
        }
    }

    public async Task ValidateCreateAsync(CreateStudentDto dto, Guid organizationId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        // 1. Verify User exists
        var user = await _dbContext.ApplicationUsers
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Id == dto.UserId, cancellationToken);

        if (user == null)
        {
            _logger.LogWarning("Validation failed: User '{UserId}' does not exist.", dto.UserId);
            throw new NotFoundException("User", dto.UserId);
        }

        // 2. Verify User belongs to the specified organization
        if (user.OrganizationId != organizationId)
        {
            _logger.LogWarning("Validation failed: User '{UserId}' belongs to organization '{UserOrgId}', expected '{ExpectedOrgId}'.",
                dto.UserId, user.OrganizationId, organizationId);
            throw new BadRequestException("User does not belong to the target organization.");
        }

        // 3. Verify User is not already registered as a student (1-to-1 constraint)
        var studentExistsForUser = await _dbContext.Students
            .IgnoreQueryFilters()
            .AnyAsync(s => s.UserId == dto.UserId, cancellationToken);

        if (studentExistsForUser)
        {
            _logger.LogWarning("Validation failed: User '{UserId}' is already associated with a student record.", dto.UserId);
            throw new ConflictException($"User with ID '{dto.UserId}' is already registered as a student.");
        }

        // 4. Verify StudentCode is unique within the organization
        var trimmedCode = dto.StudentCode.Trim();
        var codeExists = await _dbContext.Students
            .IgnoreQueryFilters()
            .AnyAsync(s => s.OrganizationId == organizationId && s.StudentCode == trimmedCode, cancellationToken);

        if (codeExists)
        {
            _logger.LogWarning("Validation failed: Student code '{StudentCode}' already exists in organization '{OrganizationId}'.",
                trimmedCode, organizationId);
            throw new ConflictException($"A student with code '{trimmedCode}' already exists in this organization.");
        }

        // 5. Verify User is not registered as an instructor (mutual exclusivity)
        var instructorExistsForUser = await _dbContext.Instructors
            .IgnoreQueryFilters()
            .AnyAsync(i => i.UserId == dto.UserId, cancellationToken);

        if (instructorExistsForUser)
        {
            _logger.LogWarning("Validation failed: User '{UserId}' is already registered as an instructor.", dto.UserId);
            throw new ConflictException($"User with ID '{dto.UserId}' is already registered as an instructor. A user cannot be both a student and an instructor.");
        }
    }

    public async Task ValidateUpdateAsync(Guid id, UpdateStudentDto dto, Guid organizationId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        // 1. Verify Student exists
        var student = await _dbContext.Students
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

        if (student == null)
        {
            _logger.LogWarning("Validation failed: Student '{StudentId}' was not found for update.", id);
            throw new NotFoundException("Student", id);
        }

        // 2. Verify StudentCode uniqueness within the organization (excluding self)
        var trimmedCode = dto.StudentCode.Trim();
        var codeExists = await _dbContext.Students
            .IgnoreQueryFilters()
            .AnyAsync(s => s.OrganizationId == organizationId && s.StudentCode == trimmedCode && s.Id != id, cancellationToken);

        if (codeExists)
        {
            _logger.LogWarning("Validation failed: Student code '{StudentCode}' is already used by another student in organization '{OrganizationId}'.",
                trimmedCode, organizationId);
            throw new ConflictException($"A student with code '{trimmedCode}' already exists in this organization.");
        }
    }

    public async Task ValidateDeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        // 1. Verify Student exists
        var student = await _dbContext.Students
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

        if (student == null)
        {
            _logger.LogWarning("Validation failed: Student '{StudentId}' was not found for deletion.", id);
            throw new NotFoundException("Student", id);
        }

        // 2. Verify no active dependent records (enrollments or attendance records)
        var enrollmentCount = await _dbContext.Enrollments
            .IgnoreQueryFilters()
            .CountAsync(e => e.StudentId == id, cancellationToken);

        var attendanceCount = await _dbContext.AttendanceRecords
            .IgnoreQueryFilters()
            .CountAsync(a => a.StudentId == id, cancellationToken);

        if (enrollmentCount > 0 || attendanceCount > 0)
        {
            _logger.LogWarning("Validation failed: Cannot delete student '{StudentId}' due to active dependencies ({Enrollments} enrollments, {Attendance} attendance records).",
                id, enrollmentCount, attendanceCount);
            throw new ConflictException(
                $"Cannot delete student '{student.StudentCode}' because they have associated records " +
                $"({enrollmentCount} enrollment(s), {attendanceCount} attendance record(s)). " +
                "Please remove or archive these records first.");
        }
    }
}
