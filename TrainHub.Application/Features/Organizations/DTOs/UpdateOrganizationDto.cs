using System.ComponentModel.DataAnnotations;

namespace TrainHub.Application.Features.Organizations.DTOs;

/// <summary>
/// Request DTO for updating an existing Organization.
/// </summary>
public class UpdateOrganizationDto
{
    [Required(ErrorMessage = "Organization name is required.")]
    [StringLength(200, MinimumLength = 2, ErrorMessage = "Organization name must be between 2 and 200 characters.")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Organization slug is required.")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "Organization slug must be between 2 and 100 characters.")]
    [RegularExpression(@"^[a-z0-9]+(?:-[a-z0-9]+)*$", ErrorMessage = "Slug must contain only lowercase letters, numbers, and single hyphens, and cannot start or end with a hyphen.")]
    public string Slug { get; set; } = string.Empty;

    [EmailAddress(ErrorMessage = "Invalid email format.")]
    [MaxLength(256, ErrorMessage = "Email cannot exceed 256 characters.")]
    public string? Email { get; set; }

    [Phone(ErrorMessage = "Invalid phone number format.")]
    [MaxLength(20, ErrorMessage = "Phone number cannot exceed 20 characters.")]
    public string? Phone { get; set; }

    [MaxLength(300, ErrorMessage = "Address cannot exceed 300 characters.")]
    public string? Address { get; set; }

    [Url(ErrorMessage = "Invalid logo URL format.")]
    [MaxLength(500, ErrorMessage = "Logo URL cannot exceed 500 characters.")]
    public string? LogoUrl { get; set; }

    public bool IsActive { get; set; }
}
