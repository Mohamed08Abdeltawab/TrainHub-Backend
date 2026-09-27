using TrainHub.Application.Common.Exceptions;
using TrainHub.Application.Features.Organizations.DTOs;
using TrainHub.Application.Interfaces;
using TrainHub.Domain.Models;

namespace TrainHub.Infrastructure.Services;

public class OrganizationService : IOrganizationService
{
    private readonly IUnitOfWork _unitOfWork;

    public OrganizationService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<OrganizationDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var organization = await _unitOfWork.Repository<Organization>().GetByIdAsync(id, cancellationToken);
        if (organization == null)
            throw new NotFoundException(nameof(Organization), id);

        return MapToDto(organization);
    }

    public async Task<IEnumerable<OrganizationDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var organizations = await _unitOfWork.Repository<Organization>().GetAllAsync(cancellationToken);
        return organizations.Select(MapToDto).ToList();
    }

    public async Task<OrganizationDto> CreateAsync(CreateOrganizationDto dto, CancellationToken cancellationToken = default)
    {
        var organization = new Organization
        {
            Name = dto.Name,
            Slug = dto.Slug,
            Email = dto.Email,
            Phone = dto.Phone,
            Address = dto.Address,
            LogoUrl = dto.LogoUrl,
            SubscriptionPlan = dto.SubscriptionPlan,
            SubscriptionEndsAt = dto.SubscriptionEndsAt,
            SubscriptionStatus = dto.SubscriptionStatus,
            IsActive = dto.IsActive,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _unitOfWork.Repository<Organization>().AddAsync(organization, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return MapToDto(organization);
    }

    public async Task<OrganizationDto> UpdateAsync(Guid id, UpdateOrganizationDto dto, CancellationToken cancellationToken = default)
    {
        var organization = await _unitOfWork.Repository<Organization>().GetByIdAsync(id, cancellationToken);
        if (organization == null)
            throw new NotFoundException(nameof(Organization), id);

        organization.Name = dto.Name;
        organization.Slug = dto.Slug;
        organization.Email = dto.Email;
        organization.Phone = dto.Phone;
        organization.Address = dto.Address;
        organization.LogoUrl = dto.LogoUrl;
        organization.SubscriptionPlan = dto.SubscriptionPlan;
        organization.SubscriptionEndsAt = dto.SubscriptionEndsAt;
        organization.SubscriptionStatus = dto.SubscriptionStatus;
        organization.IsActive = dto.IsActive;
        organization.UpdatedAt = DateTime.UtcNow;

        _unitOfWork.Repository<Organization>().Update(organization);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return MapToDto(organization);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var organization = await _unitOfWork.Repository<Organization>().GetByIdAsync(id, cancellationToken);
        if (organization == null)
            throw new NotFoundException(nameof(Organization), id);

        _unitOfWork.Repository<Organization>().Delete(organization);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
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
            SubscriptionPlan = organization.SubscriptionPlan,
            SubscriptionEndsAt = organization.SubscriptionEndsAt,
            SubscriptionStatus = organization.SubscriptionStatus,
            IsActive = organization.IsActive,
            CreatedAt = organization.CreatedAt,
            UpdatedAt = organization.UpdatedAt
        };
    }
}
