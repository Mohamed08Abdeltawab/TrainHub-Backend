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

    /// <summary>
    /// Existing organization ID when registering into a specific center.
    /// If omitted or Guid.Empty, OrganizationName (or Organization) must be provided to create a new center.
    /// </summary>
    public Guid? OrganizationId { get; set; }

    /// <summary>
    /// Name of the new organization to create for self-service tenant onboarding.
    /// </summary>
    [MaxLength(200)]
    public string? OrganizationName { get; set; }

    /// <summary>
    /// Alias for OrganizationName to align with frontend registration payload.
    /// </summary>
    [MaxLength(200)]
    public string? Organization { get; set; }

    /// <summary>
    /// Optional role specification. Only non-admin roles (Student, Instructor) are permitted during registration.
    /// </summary>
    [MaxLength(50)]
    public string? Role { get; set; }

    /// <summary>
    /// Optional phone number for the user.
    /// </summary>
    [Phone]
    [MaxLength(50)]
    public string? PhoneNumber { get; set; }

    /// <summary>
    /// Optional guardian phone number for student registrations.
    /// </summary>
    [Phone]
    [MaxLength(50)]
    public string? GuardianPhone { get; set; }

    /// <summary>
    /// Optional academic year / curriculum for student registrations.
    /// </summary>
    [MaxLength(100)]
    public string? AcademicYear { get; set; }

    /// <summary>
    /// Optional custom student code. If omitted, a code will be generated automatically.
    /// </summary>
    [MaxLength(50)]
    public string? StudentCode { get; set; }
}
