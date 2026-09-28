using System;
using System.Collections.Generic;
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

        if (!StudentConstants.IsValidStudentCode(studentCode))
            throw new ArgumentException(
                $"StudentCode '{studentCode}' is invalid. It must be between {StudentConstants.StudentCodeMinLength} and {StudentConstants.StudentCodeMaxLength} alphanumeric characters.",
                nameof(studentCode));

        if (!StudentConstants.IsValidGuardianPhone(guardianPhone))
            throw new ArgumentException(
                $"GuardianPhone '{guardianPhone}' exceeds {StudentConstants.GuardianPhoneMaxLength} characters or has an invalid format.",
                nameof(guardianPhone));

        if (!StudentConstants.IsValidAcademicYear(academicYear))
            throw new ArgumentException(
                $"AcademicYear exceeds {StudentConstants.AcademicYearMaxLength} characters.",
                nameof(academicYear));

        if (!StudentConstants.IsValidDateOfBirth(dateOfBirth))
            throw new ArgumentException(
                $"DateOfBirth must represent a valid age between {StudentConstants.MinAgeYears} and {StudentConstants.MaxAgeYears} years and cannot be in the future.",
                nameof(dateOfBirth));

        if (!StudentConstants.IsValidNotes(notes))
            throw new ArgumentException(
                $"Notes cannot exceed {StudentConstants.NotesMaxLength} characters.",
                nameof(notes));

        var now = DateTime.UtcNow;

        return new Student
        {
            Id = id ?? Guid.NewGuid(),
            UserId = userId,
            OrganizationId = organizationId,
            StudentCode = studentCode.Trim(),
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
        if (!StudentConstants.IsValidStudentCode(newStudentCode))
            throw new ArgumentException(
                $"StudentCode '{newStudentCode}' is invalid. It must be between {StudentConstants.StudentCodeMinLength} and {StudentConstants.StudentCodeMaxLength} alphanumeric characters.",
                nameof(newStudentCode));

        StudentCode = newStudentCode.Trim();
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Updates the student's guardian contact phone number.
    /// </summary>
    public void UpdateGuardianPhone(string? guardianPhone)
    {
        if (!StudentConstants.IsValidGuardianPhone(guardianPhone))
            throw new ArgumentException(
                $"GuardianPhone '{guardianPhone}' exceeds {StudentConstants.GuardianPhoneMaxLength} characters or has an invalid format.",
                nameof(guardianPhone));

        GuardianPhone = NormalizeString(guardianPhone);
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Updates the student's academic year.
    /// </summary>
    public void UpdateAcademicYear(string? academicYear)
    {
        if (!StudentConstants.IsValidAcademicYear(academicYear))
            throw new ArgumentException(
                $"AcademicYear exceeds {StudentConstants.AcademicYearMaxLength} characters.",
                nameof(academicYear));

        AcademicYear = NormalizeString(academicYear);
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Updates the student's date of birth.
    /// </summary>
    public void UpdateDateOfBirth(DateOnly? dateOfBirth)
    {
        if (!StudentConstants.IsValidDateOfBirth(dateOfBirth))
            throw new ArgumentException(
                $"DateOfBirth must represent a valid age between {StudentConstants.MinAgeYears} and {StudentConstants.MaxAgeYears} years and cannot be in the future.",
                nameof(dateOfBirth));

        DateOfBirth = dateOfBirth;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Updates internal administrative notes for the student.
    /// </summary>
    public void UpdateNotes(string? notes)
    {
        if (!StudentConstants.IsValidNotes(notes))
            throw new ArgumentException(
                $"Notes cannot exceed {StudentConstants.NotesMaxLength} characters.",
                nameof(notes));

        Notes = NormalizeString(notes);
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Updates all profile fields in a single operation while maintaining domain invariants.
    /// </summary>
    public void UpdateProfile(
        string? guardianPhone,
        string? academicYear,
        DateOnly? dateOfBirth,
        string? notes)
    {
        UpdateGuardianPhone(guardianPhone);
        UpdateAcademicYear(academicYear);
        UpdateDateOfBirth(dateOfBirth);
        UpdateNotes(notes);
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
        var dob = DateOfBirth.Value;

        if (dob > today)
            return null;

        var age = today.Year - dob.Year;
        if (dob > today.AddYears(-age))
            age--;

        return age;
    }

    private static string? NormalizeString(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }
}
