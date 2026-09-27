using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TrainHub.Application.Common.Exceptions;
using TrainHub.Application.Features.Organizations.DTOs;
using TrainHub.Application.Interfaces;
using TrainHub.Domain.Constants;
using TrainHub.Domain.Models;
using TrainHub.Infrastructure.Data;

namespace TrainHub.Infrastructure.Services;

/// <summary>
/// Validates business rules, data integrity, and subscription health for Organizations.
/// </summary>
public class OrganizationValidator : IOrganizationValidator
{
    private static readonly Regex SlugRegex = new(@"^[a-z0-9]+(?:-[a-z0-9]+)*$", RegexOptions.Compiled);
    private readonly TrainHubDbContext _dbContext;
    private readonly ILogger<OrganizationValidator> _logger;

    public OrganizationValidator(
        TrainHubDbContext dbContext,
        ILogger<OrganizationValidator> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task ValidateCreateAsync(CreateOrganizationDto dto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        ValidateGeneralFields(dto.Name, dto.Slug, dto.SubscriptionPlan, dto.SubscriptionStatus, dto.SubscriptionEndsAt);

        var normalizedSlug = dto.Slug.Trim().ToLowerInvariant();

        // Check globally unique slug across all tenants
        var slugExists = await _dbContext.Organizations
            .IgnoreQueryFilters()
            .AnyAsync(o => o.Slug == normalizedSlug, cancellationToken);

        if (slugExists)
        {
            _logger.LogWarning("Validation failed: Organization slug '{Slug}' is already taken.", normalizedSlug);
            throw new ConflictException($"An organization with the slug '{normalizedSlug}' already exists.");
        }
    }

    public async Task ValidateUpdateAsync(Guid id, UpdateOrganizationDto dto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        //if insert an invalid id
        var orgExists = await _dbContext.Organizations
            .IgnoreQueryFilters()
            .AnyAsync(o => o.Id == id, cancellationToken);

        if (!orgExists)
        {
            _logger.LogWarning("Validation failed: Organization '{OrganizationId}' was not found.", id);
            throw new NotFoundException(nameof(Organization), id);
        }

        ValidateGeneralFields(dto.Name, dto.Slug, dto.SubscriptionPlan, dto.SubscriptionStatus, dto.SubscriptionEndsAt);

        var normalizedSlug = dto.Slug.Trim().ToLowerInvariant();

        // Check if slug is used by another organization
        var slugExists = await _dbContext.Organizations
            .IgnoreQueryFilters()
            .AnyAsync(o => o.Slug == normalizedSlug && o.Id != id, cancellationToken);

        if (slugExists)
        {
            _logger.LogWarning("Validation failed: Organization slug '{Slug}' is already used by another organization.", normalizedSlug);
            throw new ConflictException($"An organization with the slug '{normalizedSlug}' already exists.");
        }
    }

    public async Task ValidateDeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var organization = await _dbContext.Organizations
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(o => o.Id == id, cancellationToken);

        if (organization == null)
        {
            _logger.LogWarning("Validation failed: Organization '{OrganizationId}' not found for deletion.", id);
            throw new NotFoundException(nameof(Organization), id);
        }

        var userCount = await _dbContext.ApplicationUsers
            .IgnoreQueryFilters()//search all students
            .CountAsync(u => u.OrganizationId == id, cancellationToken);

        var courseCount = await _dbContext.Courses
            .IgnoreQueryFilters()
            .CountAsync(c => c.OrganizationId == id, cancellationToken);

        var studentCount = await _dbContext.Students
            .IgnoreQueryFilters()
            .CountAsync(s => s.OrganizationId == id, cancellationToken);

        var instructorCount = await _dbContext.Instructors
            .IgnoreQueryFilters()
            .CountAsync(i => i.OrganizationId == id, cancellationToken);

        if (userCount > 0 || courseCount > 0 || studentCount > 0 || instructorCount > 0)//if any table has rows then trow conflict
        {
            _logger.LogWarning("Validation failed: Cannot delete organization '{OrganizationId}' due to active dependencies.", id);
            throw new ConflictException(
                $"Cannot delete organization '{organization.Name}' because it has associated resources " +
                $"({userCount} user(s), {courseCount} course(s), {studentCount} student(s), {instructorCount} instructor(s)). " +
                $"Please remove or reassign these resources first.");
        }
    }

    public async Task ValidateOrganizationActiveAsync(Guid organizationId, CancellationToken cancellationToken = default)
    {
        var organization = await _dbContext.Organizations
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(o => o.Id == organizationId, cancellationToken);

        if (organization == null)
        {
            _logger.LogWarning("Validation failed: Organization '{OrganizationId}' not found.", organizationId);
            throw new NotFoundException("Organization", organizationId);
        }

        if (!organization.IsActive)
        {
            _logger.LogWarning("Validation failed: Organization '{OrganizationId}' is inactive.", organizationId);
            throw new BadRequestException($"Organization '{organization.Name}' is currently inactive.");
        }

        if (string.Equals(organization.SubscriptionStatus, OrganizationConstants.Statuses.Suspended, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("Validation failed: Organization '{OrganizationId}' is suspended.", organizationId);
            throw new BadRequestException($"Organization '{organization.Name}' subscription is suspended.");
        }

        if (string.Equals(organization.SubscriptionStatus, OrganizationConstants.Statuses.Canceled, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("Validation failed: Organization '{OrganizationId}' subscription is canceled.", organizationId);
            throw new BadRequestException($"Organization '{organization.Name}' subscription has been canceled.");
        }

        if (string.Equals(organization.SubscriptionStatus, OrganizationConstants.Statuses.Expired, StringComparison.OrdinalIgnoreCase) ||
            organization.SubscriptionEndsAt < DateTime.UtcNow)
        {
            _logger.LogWarning("Validation failed: Organization '{OrganizationId}' subscription expired on {Expiry}.", organizationId, organization.SubscriptionEndsAt);
            throw new BadRequestException($"Organization '{organization.Name}' subscription has expired.");
        }
    }

    private static void ValidateGeneralFields(string name, string slug, string plan, string status, DateTime subscriptionEndsAt)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ValidationException(nameof(name), "Organization name cannot be empty or whitespace.");
        }

        var normalizedSlug = (slug ?? string.Empty).Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(normalizedSlug) || !SlugRegex.IsMatch(normalizedSlug))
        {
            throw new ValidationException(
                nameof(slug),
                "Slug must contain only lowercase alphanumeric characters and hyphens, and cannot start or end with a hyphen.");
        }

        if (!OrganizationConstants.Plans.IsValidPlan(plan))
        {
            throw new ValidationException(
                nameof(plan),
                $"Subscription plan '{plan}' is not supported. Allowed plans are: {string.Join(", ", OrganizationConstants.Plans.All)}.");
        }

        if (!OrganizationConstants.Statuses.IsValidStatus(status))
        {
            throw new ValidationException(
                nameof(status),
                $"Subscription status '{status}' is not supported. Allowed statuses are: {string.Join(", ", OrganizationConstants.Statuses.All)}.");
        }

        if (subscriptionEndsAt == default || subscriptionEndsAt == DateTime.MinValue)
        {
            throw new ValidationException(
                nameof(subscriptionEndsAt),
                "Subscription end date must be a valid date.");
        }
    }
}
