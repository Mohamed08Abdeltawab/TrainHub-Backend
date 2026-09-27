using Microsoft.AspNetCore.Http;
using TrainHub.Application.Interfaces;

namespace TrainHub.Infrastructure.Services;

public class CurrentOrganizationService : ICurrentOrganizationService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentOrganizationService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public Guid? OrganizationId
    {
        get
        {
            var user = _httpContextAccessor.HttpContext?.User;
            var orgIdClaim = user?.FindFirst("organizationId")?.Value;

            if (Guid.TryParse(orgIdClaim, out var organizationId) && organizationId != Guid.Empty)
            {
                return organizationId;
            }

            return null;
        }
    }
}
