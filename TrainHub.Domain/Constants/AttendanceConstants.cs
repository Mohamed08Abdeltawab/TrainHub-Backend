using TrainHub.Domain.Enums;

namespace TrainHub.Domain.Constants;

/// <summary>
/// Defines constants, constraints, and validation rules for the AttendanceRecord entity.
/// </summary>
public static class AttendanceConstants
{
    public const int NotesMaxLength = 250;

    public const byte DefaultStatus = Statuses.Present;

    public const int DefaultPageNumber = 1;
    public const int DefaultPageSize = 10;
    public const int MaxPageSize = 100;

    /// <summary>
    /// Attendance status constants aligned with <see cref="AttendanceStatus"/>.
    /// </summary>
    public static class Statuses
    {
        public const byte Absent = (byte)AttendanceStatus.Absent;
        public const byte Present = (byte)AttendanceStatus.Present;
        public const byte Late = (byte)AttendanceStatus.Late;
        public const byte Excused = (byte)AttendanceStatus.Excused;

        public static readonly IReadOnlyList<byte> All = [Absent, Present, Late, Excused];

        public static bool IsValid(byte status) => Enum.IsDefined(typeof(AttendanceStatus), status);

        public static string GetName(byte status) => IsValid(status)
            ? ((AttendanceStatus)status).ToString()
            : "Unknown";
    }

    public static readonly IReadOnlyList<string> AllowedSortFields =
    [
        "MarkedAt",
        "Status",
        "StudentId",
        "SessionId"
    ];

    /// <summary>
    /// Validates whether the notes field satisfies length constraints when provided.
    /// </summary>
    public static bool IsValidNotes(string? notes)
    {
        if (string.IsNullOrWhiteSpace(notes))
            return true;

        return notes.Trim().Length <= NotesMaxLength;
    }

    /// <summary>
    /// Validates whether an attendance status is recognized.
    /// </summary>
    public static bool IsValidStatus(byte status) => Statuses.IsValid(status);

    /// <summary>
    /// Validates whether a sort field name is recognized and supported.
    /// </summary>
    public static bool IsValidSortField(string? sortField)
    {
        if (string.IsNullOrWhiteSpace(sortField))
            return true;

        return AllowedSortFields.Contains(sortField.Trim(), StringComparer.OrdinalIgnoreCase);
    }
}
