using System.Runtime.CompilerServices;
using TrainHub.Domain.Constants;
using TrainHub.Domain.Enums;

namespace TrainHub.Domain.Models;

/// <summary>
/// Represents an AttendanceRecord entity tracking a student's attendance for a session within an Organization.
/// </summary>
public partial class AttendanceRecord
{
    public Guid Id { get; private set; }

    public Guid OrganizationId { get; private set; }

    public Guid SessionId { get; private set; }

    public Guid StudentId { get; private set; }

    public Guid? MarkedByUserId { get; private set; }

    public byte Status { get; private set; }

    public AttendanceStatus AttendanceStatus => (AttendanceStatus)Status;

    public DateTime MarkedAt { get; private set; }

    public string? Notes { get; private set; }

    public virtual ApplicationUser? MarkedByUser { get; private set; }

    public virtual Organization Organization { get; private set; } = null!;

    public virtual Session Session { get; private set; } = null!;

    public virtual Student Student { get; private set; } = null!;

    /// <summary>
    /// Parameterless constructor required for EF Core materialization.
    /// Protected to enforce creation via <see cref="Create"/>.
    /// </summary>
    protected AttendanceRecord()
    {
    }

    /// <summary>
    /// Factory method to create a new AttendanceRecord entity while validating domain invariants.
    /// </summary>
    public static AttendanceRecord Create(
        Guid organizationId,
        Guid sessionId,
        Guid studentId,
        byte status = AttendanceConstants.DefaultStatus,
        Guid? markedByUserId = null,
        string? notes = null,
        DateTime? markedAt = null,
        Guid? id = null)
    {
        if (organizationId == Guid.Empty)
            throw new ArgumentException("OrganizationId cannot be empty.", nameof(organizationId));

        if (sessionId == Guid.Empty)
            throw new ArgumentException("SessionId cannot be empty.", nameof(sessionId));

        if (studentId == Guid.Empty)
            throw new ArgumentException("StudentId cannot be empty.", nameof(studentId));

        if (markedByUserId.HasValue && markedByUserId.Value == Guid.Empty)
            throw new ArgumentException("MarkedByUserId cannot be empty GUID.", nameof(markedByUserId));

        GuardStatus(status);
        GuardNotes(notes);

        var now = DateTime.UtcNow;

        return new AttendanceRecord
        {
            Id = id ?? Guid.NewGuid(),
            OrganizationId = organizationId,
            SessionId = sessionId,
            StudentId = studentId,
            MarkedByUserId = markedByUserId,
            Status = status,
            Notes = NormalizeString(notes),
            MarkedAt = markedAt ?? now
        };
    }

    /// <summary>
    /// Updates attendance details while maintaining domain invariants.
    /// </summary>
    public void UpdateDetails(byte? status = null, string? notes = null, Guid? markedByUserId = null)
    {
        if (status.HasValue)
        {
            GuardStatus(status.Value);
            Status = status.Value;
        }

        if (notes != null)
        {
            GuardNotes(notes);
            Notes = NormalizeString(notes);
        }

        if (markedByUserId.HasValue)
        {
            if (markedByUserId.Value == Guid.Empty)
                throw new ArgumentException("MarkedByUserId cannot be empty GUID.", nameof(markedByUserId));

            MarkedByUserId = markedByUserId.Value;
        }

        MarkedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Updates the status of this attendance record.
    /// </summary>
    public void UpdateStatus(byte newStatus, Guid? markedByUserId = null)
    {
        GuardStatus(newStatus);
        Status = newStatus;

        if (markedByUserId.HasValue)
        {
            if (markedByUserId.Value == Guid.Empty)
                throw new ArgumentException("MarkedByUserId cannot be empty GUID.", nameof(markedByUserId));

            MarkedByUserId = markedByUserId.Value;
        }

        MarkedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Updates the status using the strongly-typed <see cref="AttendanceStatus"/> enum.
    /// </summary>
    public void UpdateStatus(AttendanceStatus newStatus, Guid? markedByUserId = null) =>
        UpdateStatus((byte)newStatus, markedByUserId);

    /// <summary>
    /// Marks the student as present.
    /// </summary>
    public void MarkPresent(Guid? markedByUserId = null) =>
        UpdateStatus(AttendanceConstants.Statuses.Present, markedByUserId);

    /// <summary>
    /// Marks the student as absent.
    /// </summary>
    public void MarkAbsent(Guid? markedByUserId = null) =>
        UpdateStatus(AttendanceConstants.Statuses.Absent, markedByUserId);

    /// <summary>
    /// Marks the student as late.
    /// </summary>
    public void MarkLate(Guid? markedByUserId = null) =>
        UpdateStatus(AttendanceConstants.Statuses.Late, markedByUserId);

    /// <summary>
    /// Marks the student as excused.
    /// </summary>
    public void MarkExcused(Guid? markedByUserId = null) =>
        UpdateStatus(AttendanceConstants.Statuses.Excused, markedByUserId);

    /// <summary>
    /// Updates the notes of this attendance record.
    /// </summary>
    public void UpdateNotes(string? notes)
    {
        GuardNotes(notes);
        Notes = NormalizeString(notes);
    }

    #region Guard Methods

    private static void GuardStatus(
        byte status,
        [CallerArgumentExpression(nameof(status))] string? paramName = null)
    {
        if (!AttendanceConstants.IsValidStatus(status))
            throw new ArgumentException(
                $"Status '{status}' is invalid. Allowed statuses are: {string.Join(", ", AttendanceConstants.Statuses.All)}.",
                paramName);
    }

    private static void GuardNotes(
        string? notes,
        [CallerArgumentExpression(nameof(notes))] string? paramName = null)
    {
        if (!AttendanceConstants.IsValidNotes(notes))
            throw new ArgumentException(
                $"Notes exceeds maximum length of {AttendanceConstants.NotesMaxLength} characters.",
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
