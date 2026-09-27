namespace TrainHub.Application.Features.Organizations.DTOs;

public class CreateOrganizationDto
{
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public string? LogoUrl { get; set; }
    public string SubscriptionPlan { get; set; } = string.Empty;
    public DateTime SubscriptionEndsAt { get; set; }
    public string SubscriptionStatus { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}
