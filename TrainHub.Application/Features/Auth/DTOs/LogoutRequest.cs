using System.ComponentModel.DataAnnotations;

namespace TrainHub.Application.Features.Auth.DTOs;

/// <summary>
/// Request DTO for user logout.
/// </summary>
public class LogoutRequest
{
    [Required]
    public Guid UserId { get; set; }

    [Required]
    public string RefreshToken { get; set; } = string.Empty;
}
