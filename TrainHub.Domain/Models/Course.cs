using System.Runtime.CompilerServices;
using TrainHub.Domain.Constants;

namespace TrainHub.Domain.Models;

/// <summary>
/// Represents a Course entity offered by an Organization.
/// </summary>
public partial class Course
{
    public Guid Id { get; private set; }

    public Guid OrganizationId { get; private set; }

    public string Name { get; private set; } = null!;

    public string? Description { get; private set; }

    public string? Category { get; private set; }

    public decimal BasePrice { get; private set; }

    public int? DurationInHours { get; private set; }

    public string? ThumbnailUrl { get; private set; }

    public byte Status { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public DateTime UpdatedAt { get; private set; }

    public virtual ICollection<CourseGroup> CourseGroups { get; private set; } = new List<CourseGroup>();

    public virtual Organization Organization { get; private set; } = null!;

    /// <summary>
    /// Parameterless constructor required for EF Core materialization.
    /// Protected to enforce creation via <see cref="Create"/>.
    /// </summary>
    protected Course()
    {
    }

    /// <summary>
    /// Factory method to create a new Course entity while validating domain invariants.
    /// </summary>
    public static Course Create(
        Guid organizationId,
        string name,
        string? description = null,
        string? category = null,
        decimal basePrice = 0,
        int? durationInHours = null,
        string? thumbnailUrl = null,
        byte status = CourseConstants.Statuses.Active,
        Guid? id = null)
    {
        if (organizationId == Guid.Empty)
            throw new ArgumentException("OrganizationId cannot be empty.", nameof(organizationId));

        GuardName(name);
        GuardDescription(description);
        GuardCategory(category);
        GuardBasePrice(basePrice);
        GuardDurationInHours(durationInHours);
        GuardThumbnailUrl(thumbnailUrl);
        GuardStatus(status);

        var now = DateTime.UtcNow;

        return new Course
        {
            Id = id ?? Guid.NewGuid(),
            OrganizationId = organizationId,
            Name = NormalizeName(name),
            Description = NormalizeString(description),
            Category = NormalizeCategory(category),
            BasePrice = basePrice,
            DurationInHours = durationInHours,
            ThumbnailUrl = NormalizeString(thumbnailUrl),
            Status = status,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    /// <summary>
    /// Updates course details while maintaining domain invariants.
    /// Sets UpdatedAt during this atomic operation.
    /// </summary>
    public void UpdateDetails(
        string name,
        string? description,
        string? category,
        decimal basePrice,
        int? durationInHours,
        string? thumbnailUrl,
        byte? status = null)
    {
        GuardName(name);
        GuardDescription(description);
        GuardCategory(category);
        GuardBasePrice(basePrice);
        GuardDurationInHours(durationInHours);
        GuardThumbnailUrl(thumbnailUrl);

        if (status.HasValue)
        {
            GuardStatus(status.Value);
            Status = status.Value;
        }

        Name = NormalizeName(name);
        Description = NormalizeString(description);
        Category = NormalizeCategory(category);
        BasePrice = basePrice;
        DurationInHours = durationInHours;
        ThumbnailUrl = NormalizeString(thumbnailUrl);
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Updates the course status to a new valid status.
    /// </summary>
    public void UpdateStatus(byte newStatus)
    {
        GuardStatus(newStatus);
        Status = newStatus;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Activates the course, making it available for group scheduling.
    /// </summary>
    public void Activate() => UpdateStatus(CourseConstants.Statuses.Active);

    /// <summary>
    /// Deactivates the course.
    /// </summary>
    public void Deactivate() => UpdateStatus(CourseConstants.Statuses.Inactive);

    /// <summary>
    /// Archives the course.
    /// </summary>
    public void Archive() => UpdateStatus(CourseConstants.Statuses.Archived);

    #region Guard Methods

    private static void GuardName(
        string? name,
        [CallerArgumentExpression(nameof(name))] string? paramName = null)
    {
        if (!CourseConstants.IsValidName(name))
            throw new ArgumentException(
                $"Course name '{name}' is invalid. It must be between {CourseConstants.NameMinLength} and {CourseConstants.NameMaxLength} characters.",
                paramName);
    }

    private static void GuardDescription(
        string? description,
        [CallerArgumentExpression(nameof(description))] string? paramName = null)
    {
        if (!CourseConstants.IsValidDescription(description))
            throw new ArgumentException(
                $"Description exceeds maximum length of {CourseConstants.DescriptionMaxLength} characters.",
                paramName);
    }

    private static void GuardCategory(
        string? category,
        [CallerArgumentExpression(nameof(category))] string? paramName = null)
    {
        if (!CourseConstants.IsValidCategory(category))
            throw new ArgumentException(
                $"Category must be between {CourseConstants.CategoryMinLength} and {CourseConstants.CategoryMaxLength} characters.",
                paramName);
    }

    private static void GuardBasePrice(
        decimal price,
        [CallerArgumentExpression(nameof(price))] string? paramName = null)
    {
        if (!CourseConstants.IsValidBasePrice(price))
            throw new ArgumentException(
                $"Base price must be between {CourseConstants.MinBasePrice} and {CourseConstants.MaxBasePrice}.",
                paramName);
    }

    private static void GuardDurationInHours(
        int? duration,
        [CallerArgumentExpression(nameof(duration))] string? paramName = null)
    {
        if (!CourseConstants.IsValidDurationInHours(duration))
            throw new ArgumentException(
                $"Duration in hours must be between {CourseConstants.MinDurationInHours} and {CourseConstants.MaxDurationInHours}.",
                paramName);
    }

    private static void GuardThumbnailUrl(
        string? url,
        [CallerArgumentExpression(nameof(url))] string? paramName = null)
    {
        if (!CourseConstants.IsValidThumbnailUrl(url))
            throw new ArgumentException(
                $"Thumbnail URL is invalid. It must be a valid HTTP or HTTPS URL not exceeding {CourseConstants.ThumbnailUrlMaxLength} characters.",
                paramName);
    }

    private static void GuardStatus(
        byte status,
        [CallerArgumentExpression(nameof(status))] string? paramName = null)
    {
        if (!CourseConstants.IsValidStatus(status))
            throw new ArgumentException(
                $"Status '{status}' is invalid. Allowed statuses are: {string.Join(", ", CourseConstants.Statuses.All)}.",
                paramName);
    }

    #endregion

    #region Normalization

    private static string NormalizeName(string name) => name.Trim();

    private static string? NormalizeCategory(string? category)
    {
        var trimmed = category?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }

    private static string? NormalizeString(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }

    #endregion
}
