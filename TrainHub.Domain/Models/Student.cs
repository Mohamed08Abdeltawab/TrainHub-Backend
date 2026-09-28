using System.Runtime.CompilerServices;
using TrainHub.Domain.Constants;

namespace TrainHub.Domain.Models;

/// <summary>
/// Represents a Student entity enrolled in an Organization.
/// </summary>
public partial class Student
{
    public Guid Id { get; private set; }

    public Guid UserId { get; private set; }

    public Guid OrganizationId { get; private set; }

    public string StudentCode { get; private set; } = null!;

    public string? GuardianPhone { get; private set; }

    public string? AcademicYear { get; private set; }

    public DateOnly? DateOfBirth { get; private set; }

    public string? Notes { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public DateTime UpdatedAt { get; private set; }

    public virtual ICollection<AttendanceRecord> AttendanceRecords { get; private set; } = new List<AttendanceRecord>();

    public virtual ICollection<Enrollment> Enrollments { get; private set; } = new List<Enrollment>();

    public virtual Organization Organization { get; private set; } = null!;

    public virtual ApplicationUser User { get; private set; } = null!;

    /// <summary>
    /// Parameterless constructor required for EF Core materialization.
    /// Protected to enforce creation via <see cref="Create"/>.
    /// </summary>
    protected Student()
    {
    }

    /// <summary>
    /// Factory method to create a new Student entity while validating domain invariants.
    /// </summary>
    public static Student Create(
        Guid userId,
        Guid organizationId,
        string studentCode,
        string? guardianPhone = null,
        string? academicYear = null,
        DateOnly? dateOfBirth = null,
        string? notes = null,
        Guid? id = null)
    {
        if (userId == Guid.Empty)
            throw new ArgumentException("UserId cannot be empty.", nameof(userId));

        if (organizationId == Guid.Empty)
            throw new ArgumentException("OrganizationId cannot be empty.", nameof(organizationId));

        GuardStudentCode(studentCode);
        GuardGuardianPhone(guardianPhone);
        GuardAcademicYear(academicYear);
        GuardDateOfBirth(dateOfBirth);
        GuardNotes(notes);

        var now = DateTime.UtcNow;

        return new Student
        {
            Id = id ?? Guid.NewGuid(),
            UserId = userId,
            OrganizationId = organizationId,
            StudentCode = NormalizeStudentCode(studentCode),
            GuardianPhone = NormalizeString(guardianPhone),
            AcademicYear = NormalizeString(academicYear),
            DateOfBirth = dateOfBirth,
            Notes = NormalizeString(notes),
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    /// <summary>
    /// Updates the student code, ensuring it satisfies domain invariants.
    /// </summary>
    public void UpdateStudentCode(string newStudentCode)
    {
        GuardStudentCode(newStudentCode);

        StudentCode = NormalizeStudentCode(newStudentCode);
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Updates the student's guardian contact phone number.
    /// </summary>
    public void UpdateGuardianPhone(string? guardianPhone)
    {
        GuardGuardianPhone(guardianPhone);

        GuardianPhone = NormalizeString(guardianPhone);
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Updates the student's academic year.
    /// </summary>
    public void UpdateAcademicYear(string? academicYear)
    {
        GuardAcademicYear(academicYear);

        AcademicYear = NormalizeString(academicYear);
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Updates the student's date of birth.
    /// </summary>
    public void UpdateDateOfBirth(DateOnly? dateOfBirth)
    {
        GuardDateOfBirth(dateOfBirth);

        DateOfBirth = dateOfBirth;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Updates internal administrative notes for the student.
    /// </summary>
    public void UpdateNotes(string? notes)
    {
        GuardNotes(notes);

        Notes = NormalizeString(notes);
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Updates all profile fields in a single operation while maintaining domain invariants.
    /// Sets UpdatedAt only once during this single atomic operation.
    /// </summary>
    public void UpdateProfile(
        string studentCode,
        string? guardianPhone,
        string? academicYear,
        DateOnly? dateOfBirth,
        string? notes)
    {
        GuardStudentCode(studentCode);
        GuardGuardianPhone(guardianPhone);
        GuardAcademicYear(academicYear);
        GuardDateOfBirth(dateOfBirth);
        GuardNotes(notes);

        StudentCode = NormalizeStudentCode(studentCode);
        GuardianPhone = NormalizeString(guardianPhone);
        AcademicYear = NormalizeString(academicYear);
        DateOfBirth = dateOfBirth;
        Notes = NormalizeString(notes);
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Calculates the current age in years based on DateOfBirth.
    /// </summary>
    public int? CalculateAge(DateOnly? asOf = null)
    {
        if (!DateOfBirth.HasValue)
            return null;

        var today = asOf ?? DateOnly.FromDateTime(DateTime.UtcNow);

        if (DateOfBirth.Value > today)
            return null;

        return StudentConstants.CalculateAge(DateOfBirth.Value, today);
    }

    #region Guard Methods

    private static void GuardStudentCode(
        string? code,
        [CallerArgumentExpression(nameof(code))] string? paramName = null)
    {
        if (!StudentConstants.IsValidStudentCode(code))
            throw new ArgumentException(
                $"StudentCode '{code}' is invalid. It must be between {StudentConstants.StudentCodeMinLength} and {StudentConstants.StudentCodeMaxLength} alphanumeric characters.",
                paramName);
    }

    private static void GuardGuardianPhone(
        string? phone,
        [CallerArgumentExpression(nameof(phone))] string? paramName = null)
    {
        if (!StudentConstants.IsValidGuardianPhone(phone))
            throw new ArgumentException(
                $"GuardianPhone '{phone}' exceeds {StudentConstants.GuardianPhoneMaxLength} characters or has an invalid format.",
                paramName);
    }

    private static void GuardAcademicYear(
        string? year,
        [CallerArgumentExpression(nameof(year))] string? paramName = null)
    {
        if (!StudentConstants.IsValidAcademicYear(year))
            throw new ArgumentException(
                $"AcademicYear exceeds {StudentConstants.AcademicYearMaxLength} characters.",
                paramName);
    }

    private static void GuardDateOfBirth(
        DateOnly? dob,
        [CallerArgumentExpression(nameof(dob))] string? paramName = null)
    {
        if (!StudentConstants.IsValidDateOfBirth(dob))
            throw new ArgumentException(
                $"DateOfBirth must represent a valid age between {StudentConstants.MinAgeYears} and {StudentConstants.MaxAgeYears} years and cannot be in the future.",
                paramName);
    }

    private static void GuardNotes(
        string? notes,
        [CallerArgumentExpression(nameof(notes))] string? paramName = null)
    {
        if (!StudentConstants.IsValidNotes(notes))
            throw new ArgumentException(
                $"Notes cannot exceed {StudentConstants.NotesMaxLength} characters.",
                paramName);
    }

    #endregion

    #region Normalization

    private static string NormalizeStudentCode(string code) =>
        code.Trim().ToUpperInvariant();

    private static string? NormalizeString(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }

    #endregion
}
