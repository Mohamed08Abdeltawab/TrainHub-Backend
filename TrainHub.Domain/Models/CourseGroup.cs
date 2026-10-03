using System.Runtime.CompilerServices;
using TrainHub.Domain.Constants;
using TrainHub.Domain.Enums;

namespace TrainHub.Domain.Models;

/// <summary>
/// Represents a CourseGroup entity (cohort/batch) of a Course within an Organization.
/// </summary>
public partial class CourseGroup
{
    public Guid Id { get; private set; }

    public Guid OrganizationId { get; private set; }

    public Guid CourseId { get; private set; }

    public Guid? InstructorId { get; private set; }

    public string Name { get; private set; } = null!;

    public string? ScheduleDescription { get; private set; }

    public DateOnly StartDate { get; private set; }

    public DateOnly EndDate { get; private set; }

    public int Capacity { get; private set; }

    public byte Status { get; private set; }

    public CourseGroupStatus GroupStatus => (CourseGroupStatus)Status;

    public DateTime CreatedAt { get; private set; }

    public DateTime UpdatedAt { get; private set; }

    public virtual Course Course { get; private set; } = null!;

    public virtual ICollection<Enrollment> Enrollments { get; private set; } = new List<Enrollment>();

    public virtual Instructor? Instructor { get; private set; }

    public virtual Organization Organization { get; private set; } = null!;

    public virtual ICollection<Session> Sessions { get; private set; } = new List<Session>();

    /// <summary>
    /// Parameterless constructor required for EF Core materialization.
    /// Protected to enforce creation via <see cref="Create"/>.
    /// </summary>
    protected CourseGroup()
    {
    }

    /// <summary>
    /// Factory method to create a new CourseGroup entity while validating domain invariants.
    /// </summary>
    public static CourseGroup Create(
        Guid organizationId,
        Guid courseId,
        string name,
        DateOnly startDate,
        DateOnly endDate,
        string? scheduleDescription = null,
        Guid? instructorId = null,
        int capacity = CourseGroupConstants.DefaultCapacity,
        byte status = CourseGroupConstants.DefaultStatus,
        Guid? id = null)
    {
        if (organizationId == Guid.Empty)
            throw new ArgumentException("OrganizationId cannot be empty.", nameof(organizationId));

        if (courseId == Guid.Empty)
            throw new ArgumentException("CourseId cannot be empty.", nameof(courseId));

        if (instructorId.HasValue && instructorId.Value == Guid.Empty)
            throw new ArgumentException("InstructorId cannot be empty GUID.", nameof(instructorId));

        GuardName(name);
        GuardDateRange(startDate, endDate);
        GuardScheduleDescription(scheduleDescription);
        GuardCapacity(capacity);
        GuardStatus(status);

        var now = DateTime.UtcNow;

        return new CourseGroup
        {
            Id = id ?? Guid.NewGuid(),
            OrganizationId = organizationId,
            CourseId = courseId,
            InstructorId = instructorId,
            Name = NormalizeName(name),
            ScheduleDescription = NormalizeString(scheduleDescription),
            StartDate = startDate,
            EndDate = endDate,
            Capacity = capacity,
            Status = status,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    /// <summary>
    /// Updates the group's details while maintaining domain invariants.
    /// </summary>
    public void UpdateDetails(
        string name,
        DateOnly startDate,
        DateOnly endDate,
        string? scheduleDescription = null,
        Guid? instructorId = null,
        int? capacity = null,
        byte? status = null)
    {
        GuardName(name);
        GuardDateRange(startDate, endDate);
        GuardScheduleDescription(scheduleDescription);

        if (instructorId.HasValue && instructorId.Value == Guid.Empty)
            throw new ArgumentException("InstructorId cannot be empty GUID.", nameof(instructorId));

        if (capacity.HasValue)
        {
            GuardCapacity(capacity.Value);
            Capacity = capacity.Value;
        }

        if (status.HasValue)
        {
            GuardStatus(status.Value);
            Status = status.Value;
        }

        Name = NormalizeName(name);
        StartDate = startDate;
        EndDate = endDate;
        ScheduleDescription = NormalizeString(scheduleDescription);
        InstructorId = instructorId;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Assigns or unassigns an instructor to this course group.
    /// </summary>
    public void AssignInstructor(Guid? instructorId)
    {
        if (instructorId.HasValue && instructorId.Value == Guid.Empty)
            throw new ArgumentException("InstructorId cannot be empty GUID.", nameof(instructorId));

        InstructorId = instructorId;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Unassigns the current instructor from this course group.
    /// </summary>
    public void UnassignInstructor() => AssignInstructor(null);

    /// <summary>
    /// Updates the student capacity of this course group.
    /// </summary>
    public void UpdateCapacity(int newCapacity)
    {
        GuardCapacity(newCapacity);
        Capacity = newCapacity;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Updates the group status to a new valid status.
    /// </summary>
    public void UpdateStatus(byte newStatus)
    {
        GuardStatus(newStatus);
        Status = newStatus;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Updates the group status using the strongly-typed <see cref="CourseGroupStatus"/> enum.
    /// </summary>
    public void UpdateStatus(CourseGroupStatus newStatus) => UpdateStatus((byte)newStatus);

    /// <summary>
    /// Activates the course group, making it available for enrollment and sessions.
    /// </summary>
    public void Activate() => UpdateStatus(CourseGroupConstants.Statuses.Active);

    /// <summary>
    /// Marks the course group as completed.
    /// </summary>
    public void Complete() => UpdateStatus(CourseGroupConstants.Statuses.Completed);

    /// <summary>
    /// Marks the course group as cancelled.
    /// </summary>
    public void Cancel() => UpdateStatus(CourseGroupConstants.Statuses.Cancelled);

    #region Guard Methods

    private static void GuardName(
        string? name,
        [CallerArgumentExpression(nameof(name))] string? paramName = null)
    {
        if (!CourseGroupConstants.IsValidName(name))
            throw new ArgumentException(
                $"Course group name '{name}' is invalid. It must be between {CourseGroupConstants.NameMinLength} and {CourseGroupConstants.NameMaxLength} characters.",
                paramName);
    }

    private static void GuardDateRange(
        DateOnly startDate,
        DateOnly endDate,
        [CallerArgumentExpression(nameof(endDate))] string? paramName = null)
    {
        if (!CourseGroupConstants.IsValidDateRange(startDate, endDate))
            throw new ArgumentException(
                $"EndDate '{endDate}' cannot be earlier than StartDate '{startDate}'.",
                paramName);
    }

    private static void GuardScheduleDescription(
        string? description,
        [CallerArgumentExpression(nameof(description))] string? paramName = null)
    {
        if (!CourseGroupConstants.IsValidScheduleDescription(description))
            throw new ArgumentException(
                $"ScheduleDescription exceeds maximum length of {CourseGroupConstants.ScheduleDescriptionMaxLength} characters.",
                paramName);
    }

    private static void GuardCapacity(
        int capacity,
        [CallerArgumentExpression(nameof(capacity))] string? paramName = null)
    {
        if (!CourseGroupConstants.IsValidCapacity(capacity))
            throw new ArgumentException(
                $"Capacity must be between {CourseGroupConstants.MinCapacity} and {CourseGroupConstants.MaxCapacity}.",
                paramName);
    }

    private static void GuardStatus(
        byte status,
        [CallerArgumentExpression(nameof(status))] string? paramName = null)
    {
        if (!CourseGroupConstants.IsValidStatus(status))
            throw new ArgumentException(
                $"Status '{status}' is invalid. Allowed statuses are: {string.Join(", ", CourseGroupConstants.Statuses.All)}.",
                paramName);
    }

    #endregion

    #region Normalization

    private static string NormalizeName(string name) => name.Trim();

    private static string? NormalizeString(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }

    #endregion
}
