using System.Security.Claims;

namespace TrainHub.Application.Interfaces;

/// <summary>
/// Service contract for validating instructor-related authorization policies, role checks, and resource ownership.
/// Enforces the Principle of Least Privilege: Instructors can never view or modify other instructors' data.
/// Only CenterAdmin (scoped to their organization) and SuperAdmin are authorized to manage instructors.
/// </summary>
public interface IInstructorAuthorizationService
{
    /// <summary>
    /// Checks whether the user is authorized to manage instructors (SuperAdmin or CenterAdmin/OrgAdmin).
    /// </summary>
    bool CanManageInstructors(ClaimsPrincipal user);

    /// <summary>
    /// Asserts that the user is authorized to manage instructors; throws <see cref="Common.Exceptions.ForbiddenAccessException"/> if not.
    /// </summary>
    void EnsureCanManageInstructors(ClaimsPrincipal user);

    /// <summary>
    /// Checks whether the user is authorized to view instructor lists and records (SuperAdmin or CenterAdmin/OrgAdmin).
    /// An instructor must NEVER have permission to view other instructors' data.
    /// </summary>
    bool CanViewInstructors(ClaimsPrincipal user);

    /// <summary>
    /// Checks whether the user has permission to modify a specific instructor's profile.
    /// Returns true if the user is CenterAdmin/SuperAdmin or if the user is the instructor themselves.
    /// Returns false if an instructor attempts to modify another instructor's record.
    /// </summary>
    bool CanModifyInstructor(ClaimsPrincipal user, Guid instructorUserId);

    /// <summary>
    /// Asserts that the user has permission to modify the target instructor; throws <see cref="Common.Exceptions.ForbiddenAccessException"/> if not.
    /// </summary>
    void EnsureCanModifyInstructor(ClaimsPrincipal user, Guid instructorUserId);
}
