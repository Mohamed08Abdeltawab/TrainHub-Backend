using System.Runtime.CompilerServices;
using TrainHub.Domain.Constants;
using TrainHub.Domain.Enums;

namespace TrainHub.Domain.Models;

/// <summary>
/// Represents an Enrollment entity linking a Student to a CourseGroup within an Organization.
/// </summary>
public partial class Enrollment
{
    public Guid Id { get; private set; }

    public Guid OrganizationId { get; private set; }

    public Guid CourseGroupId { get; private set; }

    public Guid StudentId { get; private set; }

    public DateTime EnrollmentDate { get; private set; }

    public decimal PaidAmount { get; private set; }

    public byte Status { get; private set; }

    public EnrollmentStatus EnrollmentStatus => (EnrollmentStatus)Status;

    public DateTime CreatedAt { get; private set; }

    public DateTime UpdatedAt { get; private set; }

    public virtual CourseGroup CourseGroup { get; private set; } = null!;

    public virtual Organization Organization { get; private set; } = null!;

    public virtual Student Student { get; private set; } = null!;

    /// <summary>
    /// Parameterless constructor required for EF Core materialization.
    /// Protected to enforce creation via <see cref="Create"/>.
    /// </summary>
    protected Enrollment()
    {
    }

    /// <summary>
    /// Factory method to create a new Enrollment entity while validating domain invariants.
    /// </summary>
    public static Enrollment Create(
        Guid organizationId,
        Guid courseGroupId,
        Guid studentId,
        decimal paidAmount = 0m,
        byte status = EnrollmentConstants.DefaultStatus,
        DateTime? enrollmentDate = null,
        Guid? id = null)
    {
        if (organizationId == Guid.Empty)
            throw new ArgumentException("OrganizationId cannot be empty.", nameof(organizationId));

        if (courseGroupId == Guid.Empty)
            throw new ArgumentException("CourseGroupId cannot be empty.", nameof(courseGroupId));

        if (studentId == Guid.Empty)
            throw new ArgumentException("StudentId cannot be empty.", nameof(studentId));

        GuardPaidAmount(paidAmount);
        GuardStatus(status);

        var now = DateTime.UtcNow;

        return new Enrollment
        {
            Id = id ?? Guid.NewGuid(),
            OrganizationId = organizationId,
            CourseGroupId = courseGroupId,
            StudentId = studentId,
            EnrollmentDate = enrollmentDate ?? now,
            PaidAmount = paidAmount,
            Status = status,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    /// <summary>
    /// Updates the enrollment details while maintaining domain invariants.
    /// </summary>
    public void UpdateDetails(
        decimal? paidAmount = null,
        byte? status = null)
    {
        if (paidAmount.HasValue)
        {
            GuardPaidAmount(paidAmount.Value);
            PaidAmount = paidAmount.Value;
        }

        if (status.HasValue)
        {
            GuardStatus(status.Value);
            Status = status.Value;
        }

        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Updates the paid amount for this enrollment.
    /// </summary>
    public void UpdatePaidAmount(decimal paidAmount)
    {
        GuardPaidAmount(paidAmount);
        PaidAmount = paidAmount;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Updates the status of this enrollment.
    /// </summary>
    public void UpdateStatus(byte newStatus)
    {
        GuardStatus(newStatus);
        Status = newStatus;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Updates the status using the strongly-typed <see cref="EnrollmentStatus"/> enum.
    /// </summary>
    public void UpdateStatus(EnrollmentStatus newStatus) => UpdateStatus((byte)newStatus);

    /// <summary>
    /// Activates the enrollment.
    /// </summary>
    public void Activate() => UpdateStatus(EnrollmentConstants.Statuses.Active);

    /// <summary>
    /// Marks the enrollment as completed.
    /// </summary>
    public void Complete() => UpdateStatus(EnrollmentConstants.Statuses.Completed);

    /// <summary>
    /// Marks the enrollment as cancelled.
    /// </summary>
    public void Cancel() => UpdateStatus(EnrollmentConstants.Statuses.Cancelled);

    #region Guard Methods

    private static void GuardPaidAmount(
        decimal paidAmount,
        [CallerArgumentExpression(nameof(paidAmount))] string? paramName = null)
    {
        if (!EnrollmentConstants.IsValidPaidAmount(paidAmount))
            throw new ArgumentException(
                $"PaidAmount must be between {EnrollmentConstants.MinPaidAmount} and {EnrollmentConstants.MaxPaidAmount}.",
                paramName);
    }

    private static void GuardStatus(
        byte status,
        [CallerArgumentExpression(nameof(status))] string? paramName = null)
    {
        if (!EnrollmentConstants.IsValidStatus(status))
            throw new ArgumentException(
                $"Status '{status}' is invalid. Allowed statuses are: {string.Join(", ", EnrollmentConstants.Statuses.All)}.",
                paramName);
    }

    #endregion
}
