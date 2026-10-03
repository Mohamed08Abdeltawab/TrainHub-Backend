using TrainHub.Domain.Enums;

namespace TrainHub.Domain.Constants;

/// <summary>
/// Defines constants, constraints, and validation rules for the Enrollment entity.
/// </summary>
public static class EnrollmentConstants
{
    public const byte DefaultStatus = Statuses.Active;
    public const decimal MinPaidAmount = 0m;
    public const decimal MaxPaidAmount = 1_000_000m;

    public const int DefaultPageNumber = 1;
    public const int DefaultPageSize = 10;
    public const int MaxPageSize = 100;

    /// <summary>
    /// Enrollment status constants aligned with <see cref="EnrollmentStatus"/>.
    /// </summary>
    public static class Statuses
    {
        public const byte Pending = (byte)EnrollmentStatus.Pending;
        public const byte Active = (byte)EnrollmentStatus.Active;
        public const byte Completed = (byte)EnrollmentStatus.Completed;
        public const byte Cancelled = (byte)EnrollmentStatus.Cancelled;

        public static readonly IReadOnlyList<byte> All = [Pending, Active, Completed, Cancelled];

        public static bool IsValid(byte status) => Enum.IsDefined(typeof(EnrollmentStatus), status);

        public static string GetName(byte status) => IsValid(status)
            ? ((EnrollmentStatus)status).ToString()
            : "Unknown";
    }

    public static readonly IReadOnlyList<string> AllowedSortFields =
    [
        "CreatedAt",
        "UpdatedAt",
        "EnrollmentDate",
        "PaidAmount",
        "Status"
    ];

    /// <summary>
    /// Validates whether a paid amount is non-negative and within reasonable bounds.
    /// </summary>
    public static bool IsValidPaidAmount(decimal paidAmount) =>
        paidAmount is >= MinPaidAmount and <= MaxPaidAmount;

    /// <summary>
    /// Validates whether an enrollment status is recognized.
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
