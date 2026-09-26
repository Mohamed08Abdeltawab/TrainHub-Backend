using System.ComponentModel.DataAnnotations;

namespace TrainHub.Application.Features.Auth.DTOs;

/// <summary>
/// Request DTO for user registration.
/// </summary>
public class RegisterRequest
{
    [Required]
    [MaxLength(100)]
    public string FirstName { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string LastName { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    [MaxLength(256)]
    public string Email { get; set; } = string.Empty;

    [Required]
    [MinLength(8)]
    public string Password { get; set; } = string.Empty;

    [Required]
    [Compare(nameof(Password), ErrorMessage = "Passwords do not match.")]
    public string ConfirmPassword { get; set; } = string.Empty;

    [Required]
    public Guid OrganizationId { get; set; }

    /// <summary>
    /// Optional phone number for the user.
    /// </summary>
    [Phone]
    [MaxLength(50)]
    public string? PhoneNumber { get; set; }
}
