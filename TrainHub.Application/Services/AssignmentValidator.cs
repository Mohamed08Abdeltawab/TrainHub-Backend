using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TrainHub.Application.Common.Exceptions;
using TrainHub.Application.Features.Assignments.DTOs;
using TrainHub.Application.Interfaces;
using TrainHub.Domain.Constants;
using TrainHub.Domain.Models;

namespace TrainHub.Application.Services;

/// <summary>
/// Validates business rules, unique constraints, relational integrity, and instructor authorization for Assignments.
/// </summary>
public class AssignmentValidator : IAssignmentValidator
{
    private readonly ITrainHubDbContext _dbContext;
    private readonly ILogger<AssignmentValidator> _logger;
    private readonly UserManager<ApplicationUser>? _userManager;

    public AssignmentValidator(
        ITrainHubDbContext dbContext,
        ILogger<AssignmentValidator> logger,
        UserManager<ApplicationUser>? userManager = null)
    {
        _dbContext = dbContext;
        _logger = logger;
        _userManager = userManager;
    }

    public void ValidateQuery(AssignmentQueryParameters query)
    {
        ArgumentNullException.ThrowIfNull(query);

        var errors = new Dictionary<string, string[]>();

        if (query.PageNumber < 1)
        {
            errors[nameof(query.PageNumber)] = ["Page number must be at least 1."];
        }

        if (query.PageSize < 1 || query.PageSize > AssignmentConstants.MaxPageSize)
        {
            errors[nameof(query.PageSize)] = [$"Page size must be between 1 and {AssignmentConstants.MaxPageSize}."];
        }

        if (query.Status.HasValue && !AssignmentConstants.Statuses.IsValid(query.Status.Value))
        {
            errors[nameof(query.Status)] = [$"Invalid status '{query.Status.Value}'. Allowed values: {string.Join(", ", AssignmentConstants.Statuses.All)}."];
        }

        if (!string.IsNullOrWhiteSpace(query.SortBy) && !AssignmentConstants.IsValidSortField(query.SortBy))
        {
            errors[nameof(query.SortBy)] = [$"Invalid sort field '{query.SortBy}'. Allowed values: {string.Join(", ", AssignmentConstants.AllowedSortFields)}."];
        }

        if (errors.Count > 0)
        {
            _logger.LogWarning("Validation failed for assignment query parameters: {ErrorCount} errors.", errors.Count);
            throw new ValidationException(errors);
        }
    }

    public async Task ValidateCreateAsync(CreateAssignmentDto dto, Guid organizationId, Guid? currentUserId = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        if (string.IsNullOrWhiteSpace(dto.Title))
        {
            throw new ValidationException(nameof(dto.Title), "Title is required.");
        }

        var trimmedTitle = dto.Title.Trim();
        if (trimmedTitle.Length < AssignmentConstants.TitleMinLength || trimmedTitle.Length > AssignmentConstants.TitleMaxLength)
        {
            throw new ValidationException(nameof(dto.Title), $"Title must be between {AssignmentConstants.TitleMinLength} and {AssignmentConstants.TitleMaxLength} characters.");
        }

        if (dto.MaxScore < AssignmentConstants.MinScore || dto.MaxScore > AssignmentConstants.MaxScore)
        {
            throw new ValidationException(nameof(dto.MaxScore), $"MaxScore must be between {AssignmentConstants.MinScore} and {AssignmentConstants.MaxScore}.");
        }

        if (!AssignmentConstants.Statuses.IsValid(dto.Status))
        {
            throw new ValidationException(nameof(dto.Status), $"Invalid status '{dto.Status}'.");
        }

        // Verify CourseGroup exists in the active tenant organization
        var group = await _dbContext.CourseGroups
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(cg => cg.Id == dto.CourseGroupId && cg.OrganizationId == organizationId, cancellationToken);

        if (group == null)
        {
            _logger.LogWarning("Validation failed: CourseGroup '{GroupId}' not found in organization '{OrgId}'.", dto.CourseGroupId, organizationId);
            throw new NotFoundException($"CourseGroup with ID '{dto.CourseGroupId}' does not exist in organization '{organizationId}'.");
        }

        // Check instructor authorization if caller is an instructor
        await ValidateInstructorAuthorizationForCourseGroupAsync(group, currentUserId, organizationId, cancellationToken);
    }

    public async Task ValidateUpdateAsync(Guid id, UpdateAssignmentDto dto, Guid organizationId, Guid? currentUserId = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        if (string.IsNullOrWhiteSpace(dto.Title))
        {
            throw new ValidationException(nameof(dto.Title), "Title is required.");
        }

        var trimmedTitle = dto.Title.Trim();
        if (trimmedTitle.Length < AssignmentConstants.TitleMinLength || trimmedTitle.Length > AssignmentConstants.TitleMaxLength)
        {
            throw new ValidationException(nameof(dto.Title), $"Title must be between {AssignmentConstants.TitleMinLength} and {AssignmentConstants.TitleMaxLength} characters.");
        }

        if (dto.MaxScore.HasValue && (dto.MaxScore.Value < AssignmentConstants.MinScore || dto.MaxScore.Value > AssignmentConstants.MaxScore))
        {
            throw new ValidationException(nameof(dto.MaxScore), $"MaxScore must be between {AssignmentConstants.MinScore} and {AssignmentConstants.MaxScore}.");
        }

        if (dto.Status.HasValue && !AssignmentConstants.Statuses.IsValid(dto.Status.Value))
        {
            throw new ValidationException(nameof(dto.Status), $"Invalid status '{dto.Status.Value}'.");
        }

        var assignment = await _dbContext.Assignments
            .IgnoreQueryFilters()
            .Include(a => a.CourseGroup)
            .FirstOrDefaultAsync(a => a.Id == id && a.OrganizationId == organizationId, cancellationToken);

        if (assignment == null)
        {
            throw new NotFoundException($"Assignment with ID '{id}' was not found.");
        }

        await ValidateInstructorAuthorizationForCourseGroupAsync(assignment.CourseGroup, currentUserId, organizationId, cancellationToken);
    }

    public async Task ValidateDeleteAsync(Guid id, Guid organizationId, Guid? currentUserId = null, CancellationToken cancellationToken = default)
    {
        var assignment = await _dbContext.Assignments
            .IgnoreQueryFilters()
            .Include(a => a.CourseGroup)
            .FirstOrDefaultAsync(a => a.Id == id && a.OrganizationId == organizationId, cancellationToken);

        if (assignment == null)
        {
            throw new NotFoundException($"Assignment with ID '{id}' was not found.");
        }

        await ValidateInstructorAuthorizationForCourseGroupAsync(assignment.CourseGroup, currentUserId, organizationId, cancellationToken);
    }

    private async Task ValidateInstructorAuthorizationForCourseGroupAsync(
        CourseGroup courseGroup,
        Guid? currentUserId,
        Guid organizationId,
        CancellationToken cancellationToken)
    {
        if (!currentUserId.HasValue || currentUserId.Value == Guid.Empty)
            return;

        var (isOrgStaff, instructor) = await ResolveCallerPermissionsAsync(currentUserId.Value, organizationId, cancellationToken);
        if (isOrgStaff)
            return;

        if (instructor == null)
            throw new ForbiddenAccessException("No active instructor profile is associated with this user.");

        if (courseGroup.InstructorId != instructor.Id)
        {
            _logger.LogWarning("Instructor '{InstructorId}' attempted to manage assignments for course group '{CourseGroupId}' which they do not teach.",
                instructor.Id, courseGroup.Id);
            throw new ForbiddenAccessException("You are not authorized to manage assignments for this course group.");
        }
    }

    public async Task<(bool isOrgStaff, Instructor? instructor)> ResolveCallerPermissionsAsync(
        Guid userId,
        Guid organizationId,
        CancellationToken cancellationToken = default)
    {
        if (_userManager != null)
        {
            var user = await _userManager.FindByIdAsync(userId.ToString());
            if (user != null)
            {
                var isSuperAdmin = await _userManager.IsInRoleAsync(user, AppRoles.SuperAdmin);
                var isOrgAdmin = await _userManager.IsInRoleAsync(user, AppRoles.OrgAdmin);

                if (isSuperAdmin || isOrgAdmin)
                {
                    return (true, null);
                }
            }
        }

        var instructor = await _dbContext.Instructors
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(i => i.UserId == userId && i.OrganizationId == organizationId, cancellationToken);

        if (instructor != null)
        {
            return (false, instructor);
        }

        return (false, null);
    }
}
