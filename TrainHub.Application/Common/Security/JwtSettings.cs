namespace TrainHub.Application.Common.Security;

/// <summary>
/// Strongly-typed configuration settings for JWT authentication.
/// </summary>
public class JwtSettings
{
    public const string SectionName = "JwtSettings";

    /// <summary>
    /// Secret key used to sign and verify JWT tokens. Must be at least 32 characters (256 bits).
    /// </summary>
    public string Secret { get; set; } = string.Empty;

    /// <summary>
    /// Identifies the principal that issued the JWT.
    /// </summary>
    public string Issuer { get; set; } = string.Empty;

    /// <summary>
    /// Identifies the recipients that the JWT is intended for.
    /// </summary>
    public string Audience { get; set; } = string.Empty;

    /// <summary>
    /// Access token validity duration in minutes. Defaults to 60 minutes.
    /// </summary>
    public int ExpiryMinutes { get; set; } = 60;

    /// <summary>
    /// Refresh token validity duration in days. Defaults to 7 days.
    /// </summary>
    public int RefreshTokenExpiryDays { get; set; } = 7;
}
