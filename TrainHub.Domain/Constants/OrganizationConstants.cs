namespace TrainHub.Domain.Constants;

/// <summary>
/// Defines constants for Organization subscription plans and statuses.
/// </summary>
public static class OrganizationConstants
{
    public static class Plans
    {
        public const string Trial = "Trial";
        public const string Free = "Free";
        public const string Basic = "Basic";
        public const string Standard = "Standard";
        public const string Premium = "Premium";
        public const string Enterprise = "Enterprise";

        public static readonly IReadOnlyList<string> All = new[]
        {
            Trial,
            Free,
            Basic,
            Standard,
            Premium,
            Enterprise
        };

        public static bool IsValidPlan(string? plan)
        {
            if (string.IsNullOrWhiteSpace(plan)) return false;
            return All.Contains(plan.Trim(), StringComparer.OrdinalIgnoreCase);
        }
    }

    public static class Statuses
    {
        public const string Active = "Active";
        public const string Trialing = "Trialing";
        public const string PastDue = "PastDue";
        public const string Canceled = "Canceled";
        public const string Suspended = "Suspended";
        public const string Expired = "Expired";

        public static readonly IReadOnlyList<string> All = new[]
        {
            Active,
            Trialing,
            PastDue,
            Canceled,
            Suspended,
            Expired
        };

        public static bool IsValidStatus(string? status)
        {
            if (string.IsNullOrWhiteSpace(status)) return false;
            return All.Contains(status.Trim(), StringComparer.OrdinalIgnoreCase);
        }
    }
}
