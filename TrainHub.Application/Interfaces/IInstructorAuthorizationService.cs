using System.Security.Claims;

namespace TrainHub.Application.Interfaces;

/// <summary>
/// Service contract for validating instructor-related authorization policies, role checks, and resource ownership.
/// </summary>
public interface IInstructorAuthorizationService
{
    /// <summary>
    /// Checks whether the user is authorized to manage instructors (SuperAdmin or OrgAdmin).
    /// </summary>
    bool CanManageInstructors(ClaimsPrincipal user);

    /// <summary>
    /// Checks whether the user is authorized to view instructors (SuperAdmin, OrgAdmin, or Instructor).
    /// </summary>
    bool CanViewInstructors(ClaimsPrincipal user);

    /// <summary>
    /// Checks whether the user has permission to modify a specific instructor's profile.
    /// Returns true if the user is OrgStaff (SuperAdmin/OrgAdmin) or if the user is the instructor themselves.
    /// </summary>
    bool CanModifyInstructor(ClaimsPrincipal user, Guid instructorUserId);

    /// <summary>
    /// Asserts that the user has permission to modify the target instructor; throws <see cref="Common.Exceptions.ForbiddenAccessException"/> if not.
    /// </summary>
    void EnsureCanModifyInstructor(ClaimsPrincipal user, Guid instructorUserId);
}
