namespace TrainHub.Application.Features.Users.DTOs;

/// <summary>
/// Response DTO containing the current authenticated user's profile information.
/// </summary>
public class CurrentUserResponse
{
    public Guid UserId { get; set; }

    public string Email { get; set; } = string.Empty;

    public string UserName { get; set; } = string.Empty;

    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    public string? PhoneNumber { get; set; }

    public Guid OrganizationId { get; set; }

    public string OrganizationName { get; set; } = string.Empty;

    public bool IsActive { get; set; }

    public IList<string> Roles { get; set; } = new List<string>();

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}
