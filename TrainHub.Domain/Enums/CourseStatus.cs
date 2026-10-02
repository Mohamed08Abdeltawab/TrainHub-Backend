namespace TrainHub.Domain.Enums;

/// <summary>
/// Represents the lifecycle status of a Course.
/// </summary>
public enum CourseStatus : byte
{
    /// <summary>
    /// Course is in preparation/draft stage and not yet published for enrollment.
    /// </summary>
    Draft = 0,

    /// <summary>
    /// Course is active and available for group scheduling and enrollment.
    /// </summary>
    Active = 1,

    /// <summary>
    /// Course is temporarily inactive/disabled.
    /// </summary>
    Inactive = 2,

    /// <summary>
    /// Course is permanently archived/retired.
    /// </summary>
    Archived = 3
}
