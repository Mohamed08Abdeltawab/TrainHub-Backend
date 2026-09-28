using System.Text.RegularExpressions;

namespace TrainHub.Domain.Constants;

/// <summary>
/// Defines constants, constraints, and validation rules for the Student entity.
/// </summary>
public static partial class StudentConstants
{
    public const int StudentCodeMaxLength = 50;
    public const int StudentCodeMinLength = 3;
    public const int GuardianPhoneMaxLength = 20;
    public const int AcademicYearMaxLength = 100;
    public const int NotesMaxLength = 500;

    public const int MinAgeYears = 3;
    public const int MaxAgeYears = 120;

    public const string StudentCodePattern = @"^[a-zA-Z0-9_\-]+$";
    public const string GuardianPhonePattern = @"^\+?[0-9\s\-()]{7,20}$";

    [GeneratedRegex(StudentCodePattern)]
    private static partial Regex StudentCodeRegex();

    [GeneratedRegex(GuardianPhonePattern)]
    private static partial Regex GuardianPhoneRegex();

    /// <summary>
    /// Validates whether a student code satisfies length and character requirements.
    /// </summary>
    public static bool IsValidStudentCode(string? studentCode)
    {
        if (string.IsNullOrWhiteSpace(studentCode))
            return false;

        var trimmed = studentCode.Trim();
        return trimmed.Length is >= StudentCodeMinLength and <= StudentCodeMaxLength
               && StudentCodeRegex().IsMatch(trimmed);
    }

    /// <summary>
    /// Validates whether a guardian phone number satisfies formatting and length rules if provided.
    /// </summary>
    public static bool IsValidGuardianPhone(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone))
            return true; // Guardian phone is optional

        var trimmed = phone.Trim();
        return trimmed.Length <= GuardianPhoneMaxLength && GuardianPhoneRegex().IsMatch(trimmed);
    }

    /// <summary>
    /// Validates whether the student's date of birth represents a valid age in the past.
    /// </summary>
    public static bool IsValidDateOfBirth(DateOnly? dateOfBirth, DateOnly? referenceDate = null)
    {
        if (!dateOfBirth.HasValue)
            return true; // Date of birth is optional

        var today = referenceDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var dob = dateOfBirth.Value;

        if (dob > today)
            return false;

        var age = today.Year - dob.Year;
        if (dob > today.AddYears(-age))
            age--;

        return age is >= MinAgeYears and <= MaxAgeYears;
    }

    /// <summary>
    /// Validates whether an academic year string meets length constraints.
    /// </summary>
    public static bool IsValidAcademicYear(string? academicYear)
    {
        if (string.IsNullOrWhiteSpace(academicYear))
            return true;

        return academicYear.Trim().Length <= AcademicYearMaxLength;
    }

    /// <summary>
    /// Validates whether notes text meets length constraints.
    /// </summary>
    public static bool IsValidNotes(string? notes)
    {
        if (string.IsNullOrWhiteSpace(notes))
            return true;

        return notes.Trim().Length <= NotesMaxLength;
    }
}
