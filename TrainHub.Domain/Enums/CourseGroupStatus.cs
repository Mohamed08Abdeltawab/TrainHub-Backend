namespace TrainHub.Domain.Enums;

/// <summary>
/// Represents the lifecycle status of a CourseGroup (cohort/batch).
/// </summary>
public enum CourseGroupStatus : byte
{
    /// <summary>
    /// Course group is in draft/planning stage and not yet open for enrollment.
    /// </summary>
    Draft = 0,

    /// <summary>
    /// Course group is active, published, and open for enrollment and sessions.
    /// </summary>
    Active = 1,

    /// <summary>
    /// Course group has completed its curriculum and sessions.
    /// </summary>
    Completed = 2,

    /// <summary>
    /// Course group has been cancelled.
    /// </summary>
    Cancelled = 3
}
