using System.Security.Claims;
using TrainHub.Application.Common.Exceptions;
using TrainHub.Application.Interfaces;
using TrainHub.Domain.Constants;

namespace TrainHub.Infrastructure.Services;

/// <summary>
/// Implements authorization rules and resource ownership verification for Instructors.
/// </summary>
public class InstructorAuthorizationService : IInstructorAuthorizationService
{
    public bool CanManageInstructors(ClaimsPrincipal user)
    {
        if (user == null) return false;

        return user.IsInRole(AppRoles.SuperAdmin) || user.IsInRole(AppRoles.OrgAdmin);
    }

    public bool CanViewInstructors(ClaimsPrincipal user)
    {
        if (user == null) return false;

        return user.IsInRole(AppRoles.SuperAdmin) ||
               user.IsInRole(AppRoles.OrgAdmin) ||
               user.IsInRole(AppRoles.Instructor);
    }

    public bool CanModifyInstructor(ClaimsPrincipal user, Guid instructorUserId)
    {
        if (user == null) return false;

        // SuperAdmin and OrgAdmin can modify any instructor in their tenant scope
        if (CanManageInstructors(user))
            return true;

        // Instructors can only modify their own profile
        if (user.IsInRole(AppRoles.Instructor))
        {
            var currentUserId = GetUserId(user);
            return currentUserId.HasValue && currentUserId.Value == instructorUserId;
        }

        return false;
    }

    public void EnsureCanModifyInstructor(ClaimsPrincipal user, Guid instructorUserId)
    {
        if (!CanModifyInstructor(user, instructorUserId))
        {
            throw new ForbiddenAccessException("You do not have permission to modify another instructor's profile.");
        }
    }

    private static Guid? GetUserId(ClaimsPrincipal user)
    {
        var idClaim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value
                      ?? user.FindFirst("sub")?.Value;

        return Guid.TryParse(idClaim, out var userId) ? userId : null;
    }
}
