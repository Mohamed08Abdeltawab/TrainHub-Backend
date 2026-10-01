using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TrainHub.Application.Common.Exceptions;
using TrainHub.Application.Features.Instructors.DTOs;
using TrainHub.Application.Interfaces;
using TrainHub.Domain.Constants;

namespace TrainHub.Application.Services;

/// <summary>
/// Validates business rules, unique constraints, and relational integrity for Instructors.
/// </summary>
public class InstructorValidator : IInstructorValidator
{
    private readonly ITrainHubDbContext _dbContext;
    private readonly ILogger<InstructorValidator> _logger;

    public InstructorValidator(ITrainHubDbContext dbContext, ILogger<InstructorValidator> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public void ValidateQuery(InstructorQueryParameters query)
    {
        ArgumentNullException.ThrowIfNull(query);

        var errors = new Dictionary<string, string[]>();

        if (query.PageNumber < 1)
        {
            errors[nameof(query.PageNumber)] = ["Page number must be at least 1."];
        }

        if (query.PageSize < 1 || query.PageSize > InstructorConstants.MaxPageSize)
        {
            errors[nameof(query.PageSize)] = [$"Page size must be between 1 and {InstructorConstants.MaxPageSize}."];
        }

        if (!string.IsNullOrWhiteSpace(query.SearchTerm) && query.SearchTerm.Trim().Length > InstructorConstants.SearchTermMaxLength)
        {
            errors[nameof(query.SearchTerm)] = [$"Search term cannot exceed {InstructorConstants.SearchTermMaxLength} characters."];
        }

        if (!string.IsNullOrWhiteSpace(query.Specialization) && query.Specialization.Trim().Length > InstructorConstants.SpecializationMaxLength)
        {
            errors[nameof(query.Specialization)] = [$"Specialization filter cannot exceed {InstructorConstants.SpecializationMaxLength} characters."];
        }

        if (!string.IsNullOrWhiteSpace(query.SortBy) && !InstructorConstants.IsValidSortField(query.SortBy))
        {
            errors[nameof(query.SortBy)] = [$"Invalid sort field '{query.SortBy}'. Allowed values: {string.Join(", ", InstructorConstants.AllowedSortFields)}."];
        }

        if (errors.Count > 0)
        {
            _logger.LogWarning("Validation failed for instructor query parameters: {ErrorCount} errors.", errors.Count);
            throw new ValidationException(errors);
        }
    }

    public async Task ValidateCreateAsync(CreateInstructorDto dto, Guid organizationId, CancellationToken cancellationToken = default)
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

        // 3. Verify User is not already registered as an instructor (1-to-1 constraint)
        var instructorExistsForUser = await _dbContext.Instructors
            .IgnoreQueryFilters()
            .AnyAsync(i => i.UserId == dto.UserId, cancellationToken);

        if (instructorExistsForUser)
        {
            _logger.LogWarning("Validation failed: User '{UserId}' is already associated with an instructor record.", dto.UserId);
            throw new ConflictException($"User with ID '{dto.UserId}' is already registered as an instructor.");
        }
    }

    public async Task ValidateUpdateAsync(Guid id, UpdateInstructorDto dto, Guid organizationId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        // 1. Verify Instructor exists
        var instructor = await _dbContext.Instructors
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(i => i.Id == id, cancellationToken);

        if (instructor == null)
        {
            _logger.LogWarning("Validation failed: Instructor '{InstructorId}' was not found for update.", id);
            throw new NotFoundException("Instructor", id);
        }

        // 2. Verify Instructor belongs to the target organization
        if (instructor.OrganizationId != organizationId)
        {
            _logger.LogWarning("Validation failed: Instructor '{InstructorId}' belongs to organization '{OrgId}', expected '{ExpectedOrgId}'.",
                id, instructor.OrganizationId, organizationId);
            throw new BadRequestException("Instructor does not belong to the target organization.");
        }
    }

    public async Task ValidateDeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        // 1. Verify Instructor exists
        var instructor = await _dbContext.Instructors
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(i => i.Id == id, cancellationToken);

        if (instructor == null)
        {
            _logger.LogWarning("Validation failed: Instructor '{InstructorId}' was not found for deletion.", id);
            throw new NotFoundException("Instructor", id);
        }

        // 2. Verify no active dependent records (course groups or sessions)
        var courseGroupCount = await _dbContext.CourseGroups
            .IgnoreQueryFilters()
            .CountAsync(cg => cg.InstructorId == id, cancellationToken);

        var sessionCount = await _dbContext.Sessions
            .IgnoreQueryFilters()
            .CountAsync(s => s.InstructorId == id, cancellationToken);

        if (courseGroupCount > 0 || sessionCount > 0)
        {
            _logger.LogWarning("Validation failed: Cannot delete instructor '{InstructorId}' due to active dependencies ({CourseGroups} course groups, {Sessions} sessions).",
                id, courseGroupCount, sessionCount);
            throw new ConflictException(
                $"Cannot delete instructor because they have associated records " +
                $"({courseGroupCount} course group(s), {sessionCount} session(s)). " +
                "Please reassign or remove these records first.");
        }
    }

    public async Task ValidateLinkUserAsync(Guid instructorId, Guid newUserId, Guid organizationId, CancellationToken cancellationToken = default)
    {
        if (instructorId == Guid.Empty)
        {
            throw new ValidationException(nameof(instructorId), "Instructor ID cannot be empty.");
        }

        if (newUserId == Guid.Empty)
        {
            throw new ValidationException(nameof(newUserId), "User ID cannot be empty.");
        }

        // 1. Verify Instructor exists
        var instructor = await _dbContext.Instructors
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(i => i.Id == instructorId, cancellationToken);

        if (instructor == null)
        {
            _logger.LogWarning("Validation failed: Instructor '{InstructorId}' was not found for user linking.", instructorId);
            throw new NotFoundException("Instructor", instructorId);
        }

        // 2. Verify User exists
        var user = await _dbContext.ApplicationUsers
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Id == newUserId, cancellationToken);

        if (user == null)
        {
            _logger.LogWarning("Validation failed: User '{UserId}' does not exist.", newUserId);
            throw new NotFoundException("User", newUserId);
        }

        // 3. Verify User belongs to the specified organization
        if (user.OrganizationId != organizationId)
        {
            _logger.LogWarning("Validation failed: User '{UserId}' belongs to organization '{UserOrgId}', expected '{ExpectedOrgId}'.",
                newUserId, user.OrganizationId, organizationId);
            throw new BadRequestException("User does not belong to the target organization.");
        }

        // 4. Verify User is active
        if (!user.IsActive)
        {
            _logger.LogWarning("Validation failed: User '{UserId}' account is inactive.", newUserId);
            throw new BadRequestException("Cannot link an inactive user account to an instructor.");
        }

        // 5. Verify User is not already linked to another instructor (1-to-1 constraint)
        var alreadyLinked = await _dbContext.Instructors
            .IgnoreQueryFilters()
            .AnyAsync(i => i.UserId == newUserId && i.Id != instructorId, cancellationToken);

        if (alreadyLinked)
        {
            _logger.LogWarning("Validation failed: User '{UserId}' is already linked to another instructor.", newUserId);
            throw new ConflictException($"User with ID '{newUserId}' is already registered as an instructor.");
        }
    }
}
