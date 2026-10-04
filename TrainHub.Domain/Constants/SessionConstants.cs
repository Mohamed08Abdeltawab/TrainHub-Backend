using TrainHub.Domain.Enums;

namespace TrainHub.Domain.Constants;

/// <summary>
/// Defines constants, constraints, and validation rules for the Session entity.
/// </summary>
public static class SessionConstants
{
    public const int RoomMaxLength = 50;

    public const byte DefaultStatus = Statuses.Scheduled;

    public const int DefaultPageNumber = 1;
    public const int DefaultPageSize = 10;
    public const int MaxPageSize = 100;
    public const int SearchTermMaxLength = 100;

    /// <summary>
    /// Session status constants aligned with <see cref="SessionStatus"/>.
    /// </summary>
    public static class Statuses
    {
        public const byte Draft = (byte)SessionStatus.Draft;
        public const byte Scheduled = (byte)SessionStatus.Scheduled;
        public const byte Completed = (byte)SessionStatus.Completed;
        public const byte Cancelled = (byte)SessionStatus.Cancelled;

        public static readonly IReadOnlyList<byte> All = [Draft, Scheduled, Completed, Cancelled];

        public static bool IsValid(byte status) => Enum.IsDefined(typeof(SessionStatus), status);

        public static string GetName(byte status) => IsValid(status)
            ? ((SessionStatus)status).ToString()
            : "Unknown";
    }

    public static readonly IReadOnlyList<string> AllowedSortFields =
    [
        "CreatedAt",
        "UpdatedAt",
        "StartTime",
        "EndTime",
        "Room",
        "Status"
    ];

    /// <summary>
    /// Validates whether the session end time is strictly after the start time.
    /// </summary>
    public static bool IsValidTimeRange(DateTime startTime, DateTime endTime) =>
        endTime > startTime;

    /// <summary>
    /// Validates whether the room name satisfies length constraints when provided.
    /// </summary>
    public static bool IsValidRoom(string? room)
    {
        if (string.IsNullOrWhiteSpace(room))
            return true;

        return room.Trim().Length <= RoomMaxLength;
    }

    /// <summary>
    /// Validates whether a session status is recognized.
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
