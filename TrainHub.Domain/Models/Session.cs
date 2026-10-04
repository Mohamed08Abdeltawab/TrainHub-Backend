using System.Runtime.CompilerServices;
using TrainHub.Domain.Constants;
using TrainHub.Domain.Enums;

namespace TrainHub.Domain.Models;

/// <summary>
/// Represents a Session entity (individual class/lecture) of a CourseGroup within an Organization.
/// </summary>
public partial class Session
{
    public Guid Id { get; private set; }

    public Guid OrganizationId { get; private set; }

    public Guid CourseGroupId { get; private set; }

    public Guid? InstructorId { get; private set; }

    public DateTime StartTime { get; private set; }

    public DateTime EndTime { get; private set; }

    public string? Room { get; private set; }

    public byte Status { get; private set; }

    public SessionStatus SessionStatus => (SessionStatus)Status;

    public DateTime CreatedAt { get; private set; }

    public DateTime UpdatedAt { get; private set; }

    public virtual ICollection<AttendanceRecord> AttendanceRecords { get; private set; } = new List<AttendanceRecord>();

    public virtual CourseGroup CourseGroup { get; private set; } = null!;

    public virtual Instructor? Instructor { get; private set; }

    public virtual Organization Organization { get; private set; } = null!;

    /// <summary>
    /// Parameterless constructor required for EF Core materialization.
    /// Protected to enforce creation via <see cref="Create"/>.
    /// </summary>
    protected Session()
    {
    }

    /// <summary>
    /// Factory method to create a new Session entity while validating domain invariants.
    /// </summary>
    public static Session Create(
        Guid organizationId,
        Guid courseGroupId,
        DateTime startTime,
        DateTime endTime,
        string? room = null,
        Guid? instructorId = null,
        byte status = SessionConstants.DefaultStatus,
        Guid? id = null)
    {
        if (organizationId == Guid.Empty)
            throw new ArgumentException("OrganizationId cannot be empty.", nameof(organizationId));

        if (courseGroupId == Guid.Empty)
            throw new ArgumentException("CourseGroupId cannot be empty.", nameof(courseGroupId));

        if (instructorId.HasValue && instructorId.Value == Guid.Empty)
            throw new ArgumentException("InstructorId cannot be empty GUID.", nameof(instructorId));

        GuardTimeRange(startTime, endTime);
        GuardRoom(room);
        GuardStatus(status);

        var now = DateTime.UtcNow;

        return new Session
        {
            Id = id ?? Guid.NewGuid(),
            OrganizationId = organizationId,
            CourseGroupId = courseGroupId,
            InstructorId = instructorId,
            StartTime = startTime,
            EndTime = endTime,
            Room = NormalizeString(room),
            Status = status,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    /// <summary>
    /// Updates the session details while maintaining domain invariants.
    /// </summary>
    public void UpdateDetails(
        DateTime startTime,
        DateTime endTime,
        string? room = null,
        Guid? instructorId = null,
        byte? status = null)
    {
        GuardTimeRange(startTime, endTime);
        GuardRoom(room);

        if (instructorId.HasValue && instructorId.Value == Guid.Empty)
            throw new ArgumentException("InstructorId cannot be empty GUID.", nameof(instructorId));

        if (status.HasValue)
        {
            GuardStatus(status.Value);
            Status = status.Value;
        }

        StartTime = startTime;
        EndTime = endTime;
        Room = NormalizeString(room);
        InstructorId = instructorId;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Reschedules the session to new start and end times and optionally assigns a room.
    /// </summary>
    public void Reschedule(DateTime startTime, DateTime endTime, string? room = null)
    {
        GuardTimeRange(startTime, endTime);
        GuardRoom(room);

        StartTime = startTime;
        EndTime = endTime;
        Room = NormalizeString(room);
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Assigns or unassigns an instructor to this session.
    /// </summary>
    public void AssignInstructor(Guid? instructorId)
    {
        if (instructorId.HasValue && instructorId.Value == Guid.Empty)
            throw new ArgumentException("InstructorId cannot be empty GUID.", nameof(instructorId));

        InstructorId = instructorId;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Unassigns the instructor from this session.
    /// </summary>
    public void UnassignInstructor() => AssignInstructor(null);

    /// <summary>
    /// Updates the session status to a new valid status.
    /// </summary>
    public void UpdateStatus(byte newStatus)
    {
        GuardStatus(newStatus);
        Status = newStatus;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Updates the session status using the strongly-typed <see cref="SessionStatus"/> enum.
    /// </summary>
    public void UpdateStatus(SessionStatus newStatus) => UpdateStatus((byte)newStatus);

    /// <summary>
    /// Marks the session as completed.
    /// </summary>
    public void Complete() => UpdateStatus(SessionConstants.Statuses.Completed);

    /// <summary>
    /// Marks the session as cancelled.
    /// </summary>
    public void Cancel() => UpdateStatus(SessionConstants.Statuses.Cancelled);

    #region Guard Methods

    private static void GuardTimeRange(
        DateTime startTime,
        DateTime endTime,
        [CallerArgumentExpression(nameof(endTime))] string? paramName = null)
    {
        if (!SessionConstants.IsValidTimeRange(startTime, endTime))
            throw new ArgumentException(
                $"EndTime '{endTime}' must be later than StartTime '{startTime}'.",
                paramName);
    }

    private static void GuardRoom(
        string? room,
        [CallerArgumentExpression(nameof(room))] string? paramName = null)
    {
        if (!SessionConstants.IsValidRoom(room))
            throw new ArgumentException(
                $"Room exceeds maximum length of {SessionConstants.RoomMaxLength} characters.",
                paramName);
    }

    private static void GuardStatus(
        byte status,
        [CallerArgumentExpression(nameof(status))] string? paramName = null)
    {
        if (!SessionConstants.IsValidStatus(status))
            throw new ArgumentException(
                $"Status '{status}' is invalid. Allowed statuses are: {string.Join(", ", SessionConstants.Statuses.All)}.",
                paramName);
    }

    #endregion

    #region Normalization

    private static string? NormalizeString(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }

    #endregion
}
