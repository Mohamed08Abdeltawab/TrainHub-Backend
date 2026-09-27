using TrainHub.Application.Features.Organizations.DTOs;

namespace TrainHub.Application.Interfaces;

/// <summary>
/// Contract for validating Organization-related operations and business rules.
/// </summary>
public interface IOrganizationValidator
{
    /// <summary>
    /// Validates business rules before creating a new organization (e.g. slug uniqueness, supported plans, valid dates).
    /// </summary>
    Task ValidateCreateAsync(CreateOrganizationDto dto, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates business rules before updating an organization (e.g. existence, slug conflict with other organizations, plans).
    /// </summary>
    Task ValidateUpdateAsync(Guid id, UpdateOrganizationDto dto, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates business rules before deleting an organization (e.g. existence, ensuring no active dependent entities).
    /// </summary>
    Task ValidateDeleteAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates that an organization exists, is marked active, and has an active subscription.
    /// Used by auth, login, and tenant access points.
    /// </summary>
    Task ValidateOrganizationActiveAsync(Guid organizationId, CancellationToken cancellationToken = default);
}
