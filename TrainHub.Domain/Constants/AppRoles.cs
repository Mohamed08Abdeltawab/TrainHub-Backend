namespace TrainHub.Domain.Constants;

/// <summary>
/// Defines application role constants and utilities for the TrainHub platform.
/// </summary>
public static class AppRoles
{
    /// <summary>
    /// Platform-level super administrator with full system-wide privileges.
    /// </summary>
    public const string SuperAdmin = "SuperAdmin";

    /// <summary>
    /// Organization administrator managing training courses, instructors, groups, and students.
    /// </summary>
    public const string OrgAdmin = "OrgAdmin";

    /// <summary>
    /// Course instructor conducting sessions, managing course materials, and recording attendance.
    /// </summary>
    public const string Instructor = "Instructor";

    /// <summary>
    /// Student/Trainee enrolled in courses and attending sessions.
    /// </summary>
    public const string Student = "Student";

    /// <summary>
    /// List of all standard system roles.
    /// </summary>
    public static readonly IReadOnlyList<string> All = new[]
    {
        SuperAdmin,
        OrgAdmin,
        Instructor,
        Student
    };

    /// <summary>
    /// Checks if a role name is defined in the system.
    /// </summary>
    public static bool IsValidRole(string roleName)
    {
        return All.Contains(roleName, StringComparer.OrdinalIgnoreCase);
    }
}
