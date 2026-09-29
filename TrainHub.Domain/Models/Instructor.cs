using System.Runtime.CompilerServices;
using TrainHub.Domain.Constants;

namespace TrainHub.Domain.Models;

/// <summary>
/// Represents an Instructor entity associated with an Organization.
/// </summary>
public partial class Instructor
{
    public Guid Id { get; private set; }

    public Guid UserId { get; private set; }

    public Guid OrganizationId { get; private set; }

    public string Specialization { get; private set; } = null!;

    public string? Bio { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public DateTime UpdatedAt { get; private set; }

    public virtual ICollection<CourseGroup> CourseGroups { get; private set; } = new List<CourseGroup>();

    public virtual Organization Organization { get; private set; } = null!;

    public virtual ICollection<Session> Sessions { get; private set; } = new List<Session>();

    public virtual ApplicationUser User { get; private set; } = null!;

    /// <summary>
    /// Parameterless constructor required for EF Core materialization.
    /// Protected to enforce creation via <see cref="Create"/>.
    /// </summary>
    protected Instructor()
    {
    }

    /// <summary>
    /// Factory method to create a new Instructor entity while validating domain invariants.
    /// </summary>
    public static Instructor Create(
        Guid userId,
        Guid organizationId,
        string specialization,
        string? bio = null,
        Guid? id = null)
    {
        if (userId == Guid.Empty)
            throw new ArgumentException("UserId cannot be empty.", nameof(userId));

        if (organizationId == Guid.Empty)
            throw new ArgumentException("OrganizationId cannot be empty.", nameof(organizationId));

        GuardSpecialization(specialization);
        GuardBio(bio);

        var now = DateTime.UtcNow;

        return new Instructor
        {
            Id = id ?? Guid.NewGuid(),
            UserId = userId,
            OrganizationId = organizationId,
            Specialization = NormalizeSpecialization(specialization),
            Bio = NormalizeString(bio),
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    /// <summary>
    /// Updates the instructor's specialization, ensuring it satisfies domain invariants.
    /// </summary>
    public void UpdateSpecialization(string newSpecialization)
    {
        GuardSpecialization(newSpecialization);

        Specialization = NormalizeSpecialization(newSpecialization);
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Updates the instructor's professional bio.
    /// </summary>
    public void UpdateBio(string? newBio)
    {
        GuardBio(newBio);

        Bio = NormalizeString(newBio);
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Updates all profile fields in a single atomic operation while maintaining domain invariants.
    /// Sets UpdatedAt only once during this single atomic operation.
    /// </summary>
    public void UpdateProfile(string specialization, string? bio)
    {
        GuardSpecialization(specialization);
        GuardBio(bio);

        Specialization = NormalizeSpecialization(specialization);
        Bio = NormalizeString(bio);
        UpdatedAt = DateTime.UtcNow;
    }

    #region Guard Methods

    private static void GuardSpecialization(
        string? specialization,
        [CallerArgumentExpression(nameof(specialization))] string? paramName = null)
    {
        if (!InstructorConstants.IsValidSpecialization(specialization))
            throw new ArgumentException(
                $"Specialization is invalid. It must be between {InstructorConstants.SpecializationMinLength} and {InstructorConstants.SpecializationMaxLength} characters.",
                paramName);
    }

    private static void GuardBio(
        string? bio,
        [CallerArgumentExpression(nameof(bio))] string? paramName = null)
    {
        if (!InstructorConstants.IsValidBio(bio))
            throw new ArgumentException(
                $"Bio cannot exceed {InstructorConstants.BioMaxLength} characters.",
                paramName);
    }

    #endregion

    #region Normalization

    private static string NormalizeSpecialization(string specialization) =>
        specialization.Trim();

    private static string? NormalizeString(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }

    #endregion
}
