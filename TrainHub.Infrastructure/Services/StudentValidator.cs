using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TrainHub.Application.Common.Exceptions;
using TrainHub.Application.Features.Students.DTOs;
using TrainHub.Application.Interfaces;
using TrainHub.Domain.Constants;
using TrainHub.Infrastructure.Data;

namespace TrainHub.Infrastructure.Services;

/// <summary>
/// Validates business rules, unique constraints, and relational integrity for Students.
/// </summary>
public class StudentValidator : IStudentValidator
{
    private readonly TrainHubDbContext _dbContext;
    private readonly ILogger<StudentValidator> _logger;

    public StudentValidator(TrainHubDbContext dbContext, ILogger<StudentValidator> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task ValidateCreateAsync(CreateStudentDto dto, Guid organizationId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        ValidateFieldConstraints(dto.DateOfBirth, dto.GuardianPhone);

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
    }

    public async Task ValidateUpdateAsync(Guid id, UpdateStudentDto dto, Guid organizationId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        ValidateFieldConstraints(dto.DateOfBirth, dto.GuardianPhone);

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

    private static void ValidateFieldConstraints(DateOnly? dateOfBirth, string? guardianPhone)
    {
        if (!StudentConstants.IsValidDateOfBirth(dateOfBirth))
        {
            throw new ValidationException(nameof(dateOfBirth),
                $"Date of birth must represent a valid age between {StudentConstants.MinAgeYears} and {StudentConstants.MaxAgeYears} years and cannot be in the future.");
        }

        if (!StudentConstants.IsValidGuardianPhone(guardianPhone))
        {
            throw new ValidationException(nameof(guardianPhone),
                $"Guardian phone exceeds {StudentConstants.GuardianPhoneMaxLength} characters or has an invalid format.");
        }
    }
}
