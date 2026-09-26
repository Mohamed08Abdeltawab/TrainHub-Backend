using System.ComponentModel.DataAnnotations;

namespace TrainHub.Application.Features.Auth.DTOs;

/// <summary>
/// Request DTO for refreshing an access token.
/// </summary>
public class RefreshTokenRequest
{
    [Required]
    public Guid UserId { get; set; }

    [Required]
    public string RefreshToken { get; set; } = string.Empty;
}
