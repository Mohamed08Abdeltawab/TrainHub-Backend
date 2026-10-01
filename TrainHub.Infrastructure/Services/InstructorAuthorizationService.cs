using System.Security.Claims;
using TrainHub.Application.Common.Exceptions;
using TrainHub.Application.Interfaces;
using TrainHub.Domain.Constants;

namespace TrainHub.Infrastructure.Services;

/// <summary>
/// Implements authorization rules and resource ownership verification for Instructors.
/// Enforces the Principle of Least Privilege:
/// - Only CenterAdmin (scoped to their OrganizationId) and SuperAdmin are authorized to create, update, or delete instructors.
/// - An Instructor must NEVER have permission to view or modify other instructors' data.
/// - Instructors may only modify their own profile data (strictly limited to basic personal info).
/// </summary>
public class InstructorAuthorizationService : IInstructorAuthorizationService
{
    public bool CanManageInstructors(ClaimsPrincipal user)
    {
        if (user == null) return false;

        return user.IsInRole(AppRoles.SuperAdmin) || user.IsInRole(AppRoles.OrgAdmin);
    }

    public void EnsureCanManageInstructors(ClaimsPrincipal user)
    {
        if (!CanManageInstructors(user))
        {
            throw new ForbiddenAccessException("Only CenterAdmin and SuperAdmin are authorized to manage instructors.");
        }
    }

    public bool CanViewInstructors(ClaimsPrincipal user)
    {
        // An Instructor must NEVER have permission to view other instructors' data.
        // Only CenterAdmin (OrgAdmin) and SuperAdmin can view instructor lists or other instructors' records.
        return CanManageInstructors(user);
    }

    public bool CanModifyInstructor(ClaimsPrincipal user, Guid instructorUserId)
    {
        if (user == null) return false;

        // SuperAdmin and CenterAdmin (OrgAdmin) can modify instructors in their tenant scope
        if (CanManageInstructors(user))
            return true;

        // Instructors can only modify their own identity, never another instructor's
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
            throw new ForbiddenAccessException("An instructor is not permitted to view or modify other instructors' data.");
        }
    }

    private static Guid? GetUserId(ClaimsPrincipal user)
    {
        var idClaim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value
                      ?? user.FindFirst("sub")?.Value;

        return Guid.TryParse(idClaim, out var userId) ? userId : null;
    }
}
