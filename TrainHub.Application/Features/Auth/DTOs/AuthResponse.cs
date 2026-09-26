namespace TrainHub.Application.Features.Auth.DTOs;

/// <summary>
/// Response DTO returned after successful authentication (register / login).
/// </summary>
public class AuthResponse
{
    public Guid UserId { get; set; }

    public string Email { get; set; } = string.Empty;

    public string UserName { get; set; } = string.Empty;

    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    public string AccessToken { get; set; } = string.Empty;

    public string RefreshToken { get; set; } = string.Empty;

    public DateTime AccessTokenExpiry { get; set; }

    public IList<string> Roles { get; set; } = new List<string>();
}
