namespace TrainHub.Domain.Enums;

/// <summary>
/// Represents the lifecycle status of a Session.
/// </summary>
public enum SessionStatus : byte
{
    /// <summary>
    /// Session is in draft/planning stage.
    /// </summary>
    Draft = 0,

    /// <summary>
    /// Session is scheduled and active.
    /// </summary>
    Scheduled = 1,

    /// <summary>
    /// Session has been held and completed.
    /// </summary>
    Completed = 2,

    /// <summary>
    /// Session has been cancelled.
    /// </summary>
    Cancelled = 3
}
