using TrainHub.Domain.Enums;

namespace TrainHub.Domain.Constants;

/// <summary>
/// Defines constants, constraints, and validation rules for the CourseGroup entity.
/// </summary>
public static class CourseGroupConstants
{
    public const int NameMaxLength = 100;
    public const int NameMinLength = 2;
    public const int ScheduleDescriptionMaxLength = 250;

    public const int DefaultCapacity = 30;
    public const int MinCapacity = 1;
    public const int MaxCapacity = 1000;

    public const byte DefaultStatus = Statuses.Active;

    public const int DefaultPageNumber = 1;
    public const int DefaultPageSize = 10;
    public const int MaxPageSize = 100;
    public const int SearchTermMaxLength = 100;

    /// <summary>
    /// Course group status constants aligned with <see cref="TrainHub.Domain.Enums.CourseGroupStatus"/>.
    /// </summary>
    public static class Statuses
    {
        public const byte Draft = (byte)CourseGroupStatus.Draft;
        public const byte Active = (byte)CourseGroupStatus.Active;
        public const byte Completed = (byte)CourseGroupStatus.Completed;
        public const byte Cancelled = (byte)CourseGroupStatus.Cancelled;

        public static readonly IReadOnlyList<byte> All = [Draft, Active, Completed, Cancelled];

        public static bool IsValid(byte status) => Enum.IsDefined(typeof(CourseGroupStatus), status);

        public static string GetName(byte status) => IsValid(status)
            ? ((CourseGroupStatus)status).ToString()
            : "Unknown";
    }

    public static readonly IReadOnlyList<string> AllowedSortFields =
    [
        "CreatedAt",
        "UpdatedAt",
        "Name",
        "StartDate",
        "EndDate",
        "Capacity",
        "Status"
    ];

    /// <summary>
    /// Validates whether a course group name satisfies length requirements.
    /// </summary>
    public static bool IsValidName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return false;

        var trimmed = name.Trim();
        return trimmed.Length is >= NameMinLength and <= NameMaxLength;
    }

    /// <summary>
    /// Validates whether a schedule description satisfies length constraints if provided.
    /// </summary>
    public static bool IsValidScheduleDescription(string? description)
    {
        if (string.IsNullOrWhiteSpace(description))
            return true;

        return description.Trim().Length <= ScheduleDescriptionMaxLength;
    }

    /// <summary>
    /// Validates that the end date is on or after the start date.
    /// </summary>
    public static bool IsValidDateRange(DateOnly startDate, DateOnly endDate) =>
        endDate >= startDate;

    /// <summary>
    /// Validates that capacity is within allowed bounds.
    /// </summary>
    public static bool IsValidCapacity(int capacity) =>
        capacity is >= MinCapacity and <= MaxCapacity;

    /// <summary>
    /// Validates whether a course group status is recognized.
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
