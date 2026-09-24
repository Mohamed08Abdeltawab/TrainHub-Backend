namespace TrainHub.Domain.Constants;

/// <summary>
/// Defines authorization policy names used across the TrainHub API.
/// </summary>
public static class AppPolicies
{
    public const string RequireSuperAdmin = "RequireSuperAdmin";
    public const string RequireOrgAdmin = "RequireOrgAdmin";
    public const string RequireInstructor = "RequireInstructor";
    public const string RequireStudent = "RequireStudent";

    /// <summary>
    /// Requires user to be either SuperAdmin or OrgAdmin.
    /// </summary>
    public const string RequireOrgStaff = "RequireOrgStaff";

    /// <summary>
    /// Requires user to be SuperAdmin, OrgAdmin, or Instructor.
    /// </summary>
    public const string RequireCourseStaff = "RequireCourseStaff";
}
