namespace TrainHub.Application.Features.Users.DTOs;

/// <summary>
/// DTO for updating the authenticated user's own profile.
/// Strictly limited to personal contact info (no Email, Role, or Organization changes).
/// </summary>
public class UpdateCurrentUserDto
{
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? PhoneNumber { get; set; }
}
