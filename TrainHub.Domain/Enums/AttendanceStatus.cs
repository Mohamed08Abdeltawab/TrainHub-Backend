namespace TrainHub.Domain.Enums;

/// <summary>
/// Represents the attendance status of a student in a session.
/// </summary>
public enum AttendanceStatus : byte
{
    /// <summary>
    /// Student was absent.
    /// </summary>
    Absent = 0,

    /// <summary>
    /// Student was present.
    /// </summary>
    Present = 1,

    /// <summary>
    /// Student arrived late.
    /// </summary>
    Late = 2,

    /// <summary>
    /// Student's absence or late arrival was excused.
    /// </summary>
    Excused = 3
}
