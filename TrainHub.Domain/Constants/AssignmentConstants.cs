namespace TrainHub.Domain.Constants;

/// <summary>
/// Domain constants and boundary limits for Assignments.
/// </summary>
public static class AssignmentConstants
{
    public const int TitleMinLength = 2;
    public const int TitleMaxLength = 200;
    public const int DescriptionMaxLength = 1000;
    public const int MinScore = 1;
    public const int MaxScore = 1000;
    public const int DefaultMaxScore = 100;
    public const int MaxPageSize = 100;
    public const byte DefaultStatus = 1;

    public static class Statuses
    {
        public const byte Draft = 0;
        public const byte Active = 1;
        public const byte Closed = 2;

        public static readonly byte[] All = [Draft, Active, Closed];

        public static bool IsValid(byte status) => All.Contains(status);

        public static string GetName(byte status) => status switch
        {
            Draft => "Draft",
            Active => "Active",
            Closed => "Closed",
            _ => "Unknown"
        };
    }

    public static readonly string[] AllowedSortFields =
    [
        "title",
        "duedate",
        "createdat",
        "updatedat",
        "status"
    ];

    public static bool IsValidSortField(string? field) =>
        !string.IsNullOrWhiteSpace(field) &&
        AllowedSortFields.Contains(field.Trim().ToLowerInvariant());
}
