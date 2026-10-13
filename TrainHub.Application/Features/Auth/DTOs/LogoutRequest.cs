using System.ComponentModel.DataAnnotations;

namespace TrainHub.Application.Features.Auth.DTOs;

/// <summary>
/// Request DTO for user logout.
/// </summary>
public class LogoutRequest
{
    public Guid? UserId { get; set; }

    public string? RefreshToken { get; set; }
}
