namespace TrainHub.Application.Features.Roles.DTOs;

/// <summary>
/// DTO representing a platform operations or administration user.
/// </summary>
public class PlatformUserDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public bool TwoFactor { get; set; }
    public string? LastLogin { get; set; }
    public string Status { get; set; } = "Active";
}
