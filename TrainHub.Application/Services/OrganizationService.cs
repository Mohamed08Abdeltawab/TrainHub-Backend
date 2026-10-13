using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TrainHub.Application.Common.Exceptions;
using TrainHub.Application.Features.Organizations.DTOs;
using TrainHub.Application.Interfaces;
using TrainHub.Domain.Models;

namespace TrainHub.Application.Services;

/// <summary>
/// Service implementation for managing Organizations.
/// </summary>
public class OrganizationService : IOrganizationService
{
    private readonly ITrainHubDbContext _dbContext;
    private readonly IOrganizationValidator _organizationValidator;
    private readonly ILogger<OrganizationService> _logger;

    public OrganizationService(
        ITrainHubDbContext dbContext,
        IOrganizationValidator organizationValidator,
        ILogger<OrganizationService> logger)
    {
        _dbContext = dbContext;
        _organizationValidator = organizationValidator;
        _logger = logger;
    }

    public async Task<OrganizationDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Retrieving organization with ID '{OrganizationId}'.", id);

        var organization = await _dbContext.Organizations
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.Id == id, cancellationToken);

        if (organization == null)
        {
            _logger.LogWarning("Organization '{OrganizationId}' was not found.", id);
            throw new NotFoundException(nameof(Organization), id);
        }

        return MapToDto(organization);
    }

    public async Task<IEnumerable<OrganizationDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Retrieving all organizations.");

        var organizations = await _dbContext.Organizations
            .IgnoreQueryFilters()
            .AsNoTracking()
            .OrderBy(o => o.Name)
            .ToListAsync(cancellationToken);

        return organizations.Select(MapToDto).ToList();
    }

    public async Task<OrganizationDto> CreateAsync(CreateOrganizationDto dto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        _logger.LogInformation("Creating organization '{OrganizationName}' with slug '{Slug}'.", dto.Name, dto.Slug);

        // Validate business rules and uniqueness
        await _organizationValidator.ValidateCreateAsync(dto, cancellationToken);

        var organization = new Organization
        {
            Name = dto.Name.Trim(),
            Slug = dto.Slug.Trim().ToLowerInvariant(),
            Email = dto.Email?.Trim(),
            Phone = dto.Phone?.Trim(),
            Address = dto.Address?.Trim(),
            LogoUrl = dto.LogoUrl?.Trim(),
            IsActive = dto.IsActive,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _dbContext.Organizations.AddAsync(organization, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Organization '{OrganizationId}' created successfully.", organization.Id);

        return MapToDto(organization);
    }

    public async Task<OrganizationDto> UpdateAsync(Guid id, UpdateOrganizationDto dto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        _logger.LogInformation("Updating organization '{OrganizationId}'.", id);

        // Validate business rules and conflict checks
        await _organizationValidator.ValidateUpdateAsync(id, dto, cancellationToken);

        var organization = await _dbContext.Organizations
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(o => o.Id == id, cancellationToken);
        if (organization == null)
        {
            _logger.LogWarning("Organization '{OrganizationId}' was not found for update.", id);
            throw new NotFoundException(nameof(Organization), id);
        }

        organization.Name = dto.Name.Trim();
        organization.Slug = dto.Slug.Trim().ToLowerInvariant();
        organization.Email = dto.Email?.Trim();
        organization.Phone = dto.Phone?.Trim();
        organization.Address = dto.Address?.Trim();
        organization.LogoUrl = dto.LogoUrl?.Trim();
        organization.IsActive = dto.IsActive;
        organization.UpdatedAt = DateTime.UtcNow;

        _dbContext.Organizations.Update(organization);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Organization '{OrganizationId}' updated successfully.", id);

        return MapToDto(organization);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Deleting organization '{OrganizationId}'.", id);

        // Validate business rules and dependencies before deletion
        await _organizationValidator.ValidateDeleteAsync(id, cancellationToken);

        var organization = await _dbContext.Organizations
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(o => o.Id == id, cancellationToken);
        if (organization == null)
        {
            _logger.LogWarning("Organization '{OrganizationId}' was not found for deletion.", id);
            throw new NotFoundException(nameof(Organization), id);
        }

        _dbContext.Organizations.Remove(organization);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Organization '{OrganizationId}' deleted successfully.", id);
    }

    private static OrganizationDto MapToDto(Organization organization)
    {
        return new OrganizationDto
        {
            Id = organization.Id,
            Name = organization.Name,
            Slug = organization.Slug,
            Email = organization.Email,
            Phone = organization.Phone,
            Address = organization.Address,
            LogoUrl = organization.LogoUrl,
            IsActive = organization.IsActive,
            CreatedAt = organization.CreatedAt,
            UpdatedAt = organization.UpdatedAt
        };
    }
}
