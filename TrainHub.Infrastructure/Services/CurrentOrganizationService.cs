using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using TrainHub.Application.Interfaces;
using TrainHub.Domain.Constants;

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
            var context = _httpContextAccessor.HttpContext;
            var user = context?.User;

            if (user != null)
            {
                var isSuperAdmin = user.IsInRole(AppRoles.SuperAdmin) ||
                                   user.HasClaim(c => (c.Type == ClaimTypes.Role || c.Type == "role") && c.Value == AppRoles.SuperAdmin);

                if (isSuperAdmin &&
                    context?.Request.Headers.TryGetValue("X-Organization-Id", out var targetOrgHeader) == true &&
                    Guid.TryParse(targetOrgHeader, out var targetOrgId) &&
                    targetOrgId != Guid.Empty)
                {
                    return targetOrgId;
                }
            }

            var orgIdClaim = user?.FindFirst("organizationId")?.Value;

            if (Guid.TryParse(orgIdClaim, out var organizationId) && organizationId != Guid.Empty)
            {
                return organizationId;
            }

            return null;
        }
    }
}
