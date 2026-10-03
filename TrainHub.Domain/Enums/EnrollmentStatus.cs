namespace TrainHub.Domain.Enums;

/// <summary>
/// Represents the lifecycle status of an Enrollment.
/// </summary>
public enum EnrollmentStatus : byte
{
    /// <summary>
    /// Enrollment is pending confirmation or approval.
    /// </summary>
    Pending = 0,

    /// <summary>
    /// Enrollment is active and the student is actively participating in the course group.
    /// </summary>
    Active = 1,

    /// <summary>
    /// Student has successfully completed the course group.
    /// </summary>
    Completed = 2,

    /// <summary>
    /// Enrollment has been cancelled or dropped.
    /// </summary>
    Cancelled = 3
}
