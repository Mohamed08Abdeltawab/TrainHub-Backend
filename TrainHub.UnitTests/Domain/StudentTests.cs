using FluentAssertions;
using TrainHub.Domain.Constants;
using TrainHub.Domain.Models;

namespace TrainHub.UnitTests.Domain;

public class StudentTests
{
    private readonly Guid _validUserId = Guid.NewGuid();
    private readonly Guid _validOrgId = Guid.NewGuid();
    private const string ValidStudentCode = "STU-2026-001";

    [Fact]
    public void Create_WithValidParameters_ReturnsStudentInstance()
    {
        // Arrange
        var dob = new DateOnly(2005, 5, 15);
        var phone = "+1234567890";
        var year = "Senior 2026";
        var notes = "Top performer in math";

        // Act
        var student = Student.Create(
            _validUserId,
            _validOrgId,
            ValidStudentCode,
            phone,
            year,
            dob,
            notes);

        // Assert
        student.Should().NotBeNull();
        student.Id.Should().NotBeEmpty();
        student.UserId.Should().Be(_validUserId);
        student.OrganizationId.Should().Be(_validOrgId);
        student.StudentCode.Should().Be(ValidStudentCode);
        student.GuardianPhone.Should().Be(phone);
        student.AcademicYear.Should().Be(year);
        student.DateOfBirth.Should().Be(dob);
        student.Notes.Should().Be(notes);
        student.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
        student.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void Create_WithEmptyUserId_ThrowsArgumentException()
    {
        // Act
        var act = () => Student.Create(Guid.Empty, _validOrgId, ValidStudentCode);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*UserId cannot be empty*");
    }

    [Fact]
    public void Create_WithEmptyOrganizationId_ThrowsArgumentException()
    {
        // Act
        var act = () => Student.Create(_validUserId, Guid.Empty, ValidStudentCode);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*OrganizationId cannot be empty*");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("A")] // too short (< 3)
    [InlineData("STU@#$")] // invalid characters
    public void Create_WithInvalidStudentCode_ThrowsArgumentException(string invalidCode)
    {
        // Act
        var act = () => Student.Create(_validUserId, _validOrgId, invalidCode);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*StudentCode*is invalid*");
    }

    [Fact]
    public void Create_WithDateOfBirthInFuture_ThrowsArgumentException()
    {
        // Arrange
        var futureDob = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1));

        // Act
        var act = () => Student.Create(_validUserId, _validOrgId, ValidStudentCode, dateOfBirth: futureDob);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*DateOfBirth*");
    }

    [Fact]
    public void Create_WithInvalidGuardianPhone_ThrowsArgumentException()
    {
        // Arrange
        var invalidPhone = "not-a-phone-number-at-all-and-way-too-long";

        // Act
        var act = () => Student.Create(_validUserId, _validOrgId, ValidStudentCode, guardianPhone: invalidPhone);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*GuardianPhone*");
    }

    [Fact]
    public void Create_WithNotesExceedingMaxLength_ThrowsArgumentException()
    {
        // Arrange
        var longNotes = new string('N', StudentConstants.NotesMaxLength + 1);

        // Act
        var act = () => Student.Create(_validUserId, _validOrgId, ValidStudentCode, notes: longNotes);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Notes cannot exceed*");
    }

    [Fact]
    public void UpdateStudentCode_WithValidCode_UpdatesCodeAndTimestamp()
    {
        // Arrange
        var student = Student.Create(_validUserId, _validOrgId, ValidStudentCode);
        var initialUpdatedAt = student.UpdatedAt;
        var newCode = "STU-NEW-999";

        // Act
        student.UpdateStudentCode(newCode);

        // Assert
        student.StudentCode.Should().Be(newCode);
        student.UpdatedAt.Should().BeOnOrAfter(initialUpdatedAt);
    }

    [Fact]
    public void UpdateGuardianPhone_NormalizesWhitespaceAndUpdates()
    {
        // Arrange
        var student = Student.Create(_validUserId, _validOrgId, ValidStudentCode);

        // Act
        student.UpdateGuardianPhone("  +1234567890  ");

        // Assert
        student.GuardianPhone.Should().Be("+1234567890");

        // Act empty
        student.UpdateGuardianPhone("   ");
        student.GuardianPhone.Should().BeNull();
    }

    [Fact]
    public void UpdateProfile_UpdatesAllFields()
    {
        // Arrange
        var student = Student.Create(_validUserId, _validOrgId, ValidStudentCode);
        var newDob = new DateOnly(2004, 3, 20);

        // Act
        student.UpdateProfile("STU-UPDATED-001", "+9876543210", "Sophomore", newDob, "Transferred from another school");

        // Assert
        student.StudentCode.Should().Be("STU-UPDATED-001");
        student.GuardianPhone.Should().Be("+9876543210");
        student.AcademicYear.Should().Be("Sophomore");
        student.DateOfBirth.Should().Be(newDob);
        student.Notes.Should().Be("Transferred from another school");
    }

    [Fact]
    public void Create_NormalizesStudentCodeToUppercaseAndTrimmed()
    {
        // Act
        var student = Student.Create(_validUserId, _validOrgId, "  stu-lower-001  ");

        // Assert
        student.StudentCode.Should().Be("STU-LOWER-001");
    }

    [Fact]
    public void UpdateStudentCode_NormalizesStudentCodeToUppercaseAndTrimmed()
    {
        // Arrange
        var student = Student.Create(_validUserId, _validOrgId, ValidStudentCode);

        // Act
        student.UpdateStudentCode("  stu-updated-002  ");

        // Assert
        student.StudentCode.Should().Be("STU-UPDATED-002");
    }

    [Fact]
    public void UpdateProfile_NormalizesStudentCodeToUppercaseAndTrimmed()
    {
        // Arrange
        var student = Student.Create(_validUserId, _validOrgId, ValidStudentCode);

        // Act
        student.UpdateProfile("  stu-profile-003  ", null, null, null, null);

        // Assert
        student.StudentCode.Should().Be("STU-PROFILE-003");
    }

    [Fact]
    public void GuardStudentCode_ThrowsWithParameterNameCapturedByCallerArgumentExpression()
    {
        // Act
        var customCode = "invalid#code";
        var act = () => Student.Create(_validUserId, _validOrgId, customCode);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("studentCode");
    }

    [Fact]
    public void CalculateAge_WithValidDob_ReturnsCorrectAge()
    {
        // Arrange
        var dob = new DateOnly(2000, 1, 1);
        var asOf = new DateOnly(2026, 1, 1);
        var student = Student.Create(_validUserId, _validOrgId, ValidStudentCode, dateOfBirth: dob);

        // Act
        var age = student.CalculateAge(asOf);

        // Assert
        age.Should().Be(26);
    }

    [Fact]
    public void CalculateAge_WhenDobIsNull_ReturnsNull()
    {
        // Arrange
        var student = Student.Create(_validUserId, _validOrgId, ValidStudentCode);

        // Act
        var age = student.CalculateAge();

        // Assert
        age.Should().BeNull();
    }

    [Fact]
    public void Constructor_Parameterless_IsProtected()
    {
        // Assert
        var ctor = typeof(Student).GetConstructor(
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance,
            null,
            Type.EmptyTypes,
            null);

        ctor.Should().NotBeNull();
        ctor!.IsFamily.Should().BeTrue(); // protected
    }

    [Fact]
    public void PropertySetters_ArePrivate()
    {
        // Assert
        var properties = typeof(Student).GetProperties(
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

        foreach (var prop in properties)
        {
            var setMethod = prop.GetSetMethod(nonPublic: true);
            setMethod.Should().NotBeNull($"Property {prop.Name} should have a setter");
            setMethod!.IsPrivate.Should().BeTrue($"Property {prop.Name} setter should be private");
        }
    }
}

