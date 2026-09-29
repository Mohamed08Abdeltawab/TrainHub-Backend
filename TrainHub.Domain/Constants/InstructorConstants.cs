namespace TrainHub.Domain.Constants;

/// <summary>
/// Defines constants, constraints, and validation rules for the Instructor entity.
/// </summary>
public static class InstructorConstants
{
    public const int SpecializationMaxLength = 150;
    public const int SpecializationMinLength = 2;
    public const int BioMaxLength = 2000;

    public const int DefaultPageNumber = 1;
    public const int DefaultPageSize = 10;
    public const int MaxPageSize = 100;
    public const int SearchTermMaxLength = 100;

    public static readonly IReadOnlyList<string> AllowedSortFields = new[]
    {
        "CreatedAt",
        "UpdatedAt",
        "Specialization",
        "FirstName",
        "LastName",
        "Email"
    };

    /// <summary>
    /// Validates whether a specialization string satisfies length requirements.
    /// </summary>
    public static bool IsValidSpecialization(string? specialization)
    {
        if (string.IsNullOrWhiteSpace(specialization))
            return false;

        var trimmed = specialization.Trim();
        return trimmed.Length is >= SpecializationMinLength and <= SpecializationMaxLength;
    }

    /// <summary>
    /// Validates whether a bio meets length constraints if provided.
    /// </summary>
    public static bool IsValidBio(string? bio)
    {
        if (string.IsNullOrWhiteSpace(bio))
            return true;

        return bio.Trim().Length <= BioMaxLength;
    }

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
