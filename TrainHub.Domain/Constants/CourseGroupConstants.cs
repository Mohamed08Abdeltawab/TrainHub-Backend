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

    public const byte DefaultStatus = 1;

    public const int DefaultPageNumber = 1;
    public const int DefaultPageSize = 10;
    public const int MaxPageSize = 100;
    public const int SearchTermMaxLength = 100;

    public static readonly IReadOnlyList<string> AllowedSortFields =
    [
        "CreatedAt",
        "UpdatedAt",
        "Name",
        "StartDate",
        "EndDate"
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
    /// Validates whether a sort field name is recognized and supported.
    /// </summary>
    public static bool IsValidSortField(string? sortField)
    {
        if (string.IsNullOrWhiteSpace(sortField))
            return true;

        return AllowedSortFields.Contains(sortField.Trim(), StringComparer.OrdinalIgnoreCase);
    }
}
