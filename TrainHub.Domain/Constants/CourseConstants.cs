namespace TrainHub.Domain.Constants;

/// <summary>
/// Defines constants, constraints, and validation rules for the Course entity.
/// </summary>
public static class CourseConstants
{
    public const int NameMaxLength = 200;
    public const int NameMinLength = 2;
    public const int DescriptionMaxLength = 4000;
    public const int CategoryMaxLength = 100;
    public const int CategoryMinLength = 2;
    public const int ThumbnailUrlMaxLength = 500;

    public const decimal MinBasePrice = 0m;
    public const decimal MaxBasePrice = 1_000_000m;

    public const int MinDurationInHours = 1;
    public const int MaxDurationInHours = 10_000;

    public const int DefaultPageNumber = 1;
    public const int DefaultPageSize = 10;
    public const int MaxPageSize = 100;
    public const int SearchTermMaxLength = 100;

    /// <summary>
    /// Course status constants.
    /// </summary>
    public static class Statuses
    {
        public const byte Draft = 0;
        public const byte Active = 1;
        public const byte Inactive = 2;
        public const byte Archived = 3;

        public static readonly IReadOnlyList<byte> All = [Draft, Active, Inactive, Archived];

        public static bool IsValid(byte status) => All.Contains(status);

        public static string GetName(byte status) => status switch
        {
            Draft => "Draft",
            Active => "Active",
            Inactive => "Inactive",
            Archived => "Archived",
            _ => "Unknown"
        };
    }

    public static readonly IReadOnlyList<string> AllowedSortFields =
    [
        "CreatedAt",
        "UpdatedAt",
        "Name",
        "Category",
        "BasePrice",
        "DurationInHours",
        "Status"
    ];

    /// <summary>
    /// Validates whether a course name satisfies length requirements.
    /// </summary>
    public static bool IsValidName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return false;

        var trimmed = name.Trim();
        return trimmed.Length is >= NameMinLength and <= NameMaxLength;
    }

    /// <summary>
    /// Validates whether a course description satisfies length constraints if provided.
    /// </summary>
    public static bool IsValidDescription(string? description)
    {
        if (string.IsNullOrWhiteSpace(description))
            return true;

        return description.Trim().Length <= DescriptionMaxLength;
    }

    /// <summary>
    /// Validates whether a category satisfies length constraints if provided.
    /// </summary>
    public static bool IsValidCategory(string? category)
    {
        if (string.IsNullOrWhiteSpace(category))
            return true;

        var trimmed = category.Trim();
        return trimmed.Length is >= CategoryMinLength and <= CategoryMaxLength;
    }

    /// <summary>
    /// Validates whether a base price is non-negative and within reasonable bounds.
    /// </summary>
    public static bool IsValidBasePrice(decimal price) =>
        price is >= MinBasePrice and <= MaxBasePrice;

    /// <summary>
    /// Validates whether the duration in hours is valid if provided.
    /// </summary>
    public static bool IsValidDurationInHours(int? duration)
    {
        if (!duration.HasValue)
            return true;

        return duration.Value is >= MinDurationInHours and <= MaxDurationInHours;
    }

    /// <summary>
    /// Validates whether a thumbnail URL is a well-formed absolute HTTP/HTTPS URL and within length constraints if provided.
    /// </summary>
    public static bool IsValidThumbnailUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return true;

        var trimmed = url.Trim();
        if (trimmed.Length > ThumbnailUrlMaxLength)
            return false;

        return Uri.TryCreate(trimmed, UriKind.Absolute, out var uriResult)
               && (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps);
    }

    /// <summary>
    /// Validates whether a course status is recognized.
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
