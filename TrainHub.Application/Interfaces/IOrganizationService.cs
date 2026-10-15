using TrainHub.Application.Features.Organizations.DTOs;

namespace TrainHub.Application.Interfaces;

public interface IOrganizationService
{
    Task<OrganizationDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IEnumerable<OrganizationDto>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<OrganizationDto> CreateAsync(CreateOrganizationDto dto, CancellationToken cancellationToken = default);
    Task<OrganizationDto> UpdateAsync(Guid id, UpdateOrganizationDto dto, CancellationToken cancellationToken = default);
    Task<OrganizationDto> UpdateProfileAsync(Guid id, UpdateOrganizationProfileDto dto, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
