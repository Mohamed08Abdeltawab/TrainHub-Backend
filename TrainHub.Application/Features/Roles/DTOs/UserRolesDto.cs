namespace TrainHub.Application.Features.Roles.DTOs;

public class UserRolesDto
{
    public Guid UserId { get; set; }
    public string? Email { get; set; }
    public string? UserName { get; set; }
    public IReadOnlyList<string> Roles { get; set; } = Array.Empty<string>();
}
