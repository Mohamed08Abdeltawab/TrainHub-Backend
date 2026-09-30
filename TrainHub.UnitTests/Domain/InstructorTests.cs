using FluentAssertions;
using TrainHub.Domain.Constants;
using TrainHub.Domain.Models;

namespace TrainHub.UnitTests.Domain;

public class InstructorTests
{
    private readonly Guid _validUserId = Guid.NewGuid();
    private readonly Guid _validOrgId = Guid.NewGuid();
    private const string ValidSpecialization = "Computer Science & Cloud Architecture";

    [Fact]
    public void Create_WithValidParameters_ReturnsInstructorInstance()
    {
        // Arrange
        var bio = "10+ years experience in distributed systems.";

        // Act
        var instructor = Instructor.Create(
            _validUserId,
            _validOrgId,
            ValidSpecialization,
            bio);

        // Assert
        instructor.Should().NotBeNull();
        instructor.Id.Should().NotBeEmpty();
        instructor.UserId.Should().Be(_validUserId);
        instructor.OrganizationId.Should().Be(_validOrgId);
        instructor.Specialization.Should().Be(ValidSpecialization);
        instructor.Bio.Should().Be(bio);
        instructor.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
        instructor.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void Create_WithExplicitId_SetsProvidedId()
    {
        // Arrange
        var customId = Guid.NewGuid();

        // Act
        var instructor = Instructor.Create(
            _validUserId,
            _validOrgId,
            ValidSpecialization,
            id: customId);

        // Assert
        instructor.Id.Should().Be(customId);
    }

    [Fact]
    public void Create_WithEmptyUserId_ThrowsArgumentException()
    {
        // Act
        var act = () => Instructor.Create(Guid.Empty, _validOrgId, ValidSpecialization);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*UserId cannot be empty*");
    }

    [Fact]
    public void Create_WithEmptyOrganizationId_ThrowsArgumentException()
    {
        // Act
        var act = () => Instructor.Create(_validUserId, Guid.Empty, ValidSpecialization);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*OrganizationId cannot be empty*");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("A")] // too short (< 2)
    public void Create_WithInvalidSpecialization_ThrowsArgumentException(string invalidSpecialization)
    {
        // Act
        var act = () => Instructor.Create(_validUserId, _validOrgId, invalidSpecialization);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Specialization is invalid*");
    }

    [Fact]
    public void Create_WithSpecializationExceedingMaxLength_ThrowsArgumentException()
    {
        // Arrange
        var longSpecialization = new string('S', InstructorConstants.SpecializationMaxLength + 1);

        // Act
        var act = () => Instructor.Create(_validUserId, _validOrgId, longSpecialization);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Specialization is invalid*");
    }

    [Fact]
    public void Create_WithBioExceedingMaxLength_ThrowsArgumentException()
    {
        // Arrange
        var longBio = new string('B', InstructorConstants.BioMaxLength + 1);

        // Act
        var act = () => Instructor.Create(_validUserId, _validOrgId, ValidSpecialization, bio: longBio);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Bio cannot exceed*");
    }

    [Fact]
    public void UpdateSpecialization_WithValidValue_UpdatesSpecializationAndTimestamp()
    {
        // Arrange
        var instructor = Instructor.Create(_validUserId, _validOrgId, ValidSpecialization);
        var initialUpdatedAt = instructor.UpdatedAt;
        var newSpecialization = "Artificial Intelligence";

        // Act
        instructor.UpdateSpecialization(newSpecialization);

        // Assert
        instructor.Specialization.Should().Be(newSpecialization);
        instructor.UpdatedAt.Should().BeOnOrAfter(initialUpdatedAt);
    }

    [Fact]
    public void UpdateBio_NormalizesWhitespaceAndUpdates()
    {
        // Arrange
        var instructor = Instructor.Create(_validUserId, _validOrgId, ValidSpecialization);

        // Act
        instructor.UpdateBio("  Senior Software Engineer  ");

        // Assert
        instructor.Bio.Should().Be("Senior Software Engineer");

        // Act empty string -> should normalize to null
        instructor.UpdateBio("   ");
        instructor.Bio.Should().BeNull();
    }

    [Fact]
    public void UpdateProfile_UpdatesAllFields()
    {
        // Arrange
        var instructor = Instructor.Create(_validUserId, _validOrgId, ValidSpecialization);
        var initialUpdatedAt = instructor.UpdatedAt;

        // Act
        instructor.UpdateProfile("Data Science & Machine Learning", "PhD in Computer Science");

        // Assert
        instructor.Specialization.Should().Be("Data Science & Machine Learning");
        instructor.Bio.Should().Be("PhD in Computer Science");
        instructor.UpdatedAt.Should().BeOnOrAfter(initialUpdatedAt);
    }

    [Fact]
    public void Create_NormalizesSpecializationByTrimming()
    {
        // Act
        var instructor = Instructor.Create(_validUserId, _validOrgId, "   DevOps & SRE   ");

        // Assert
        instructor.Specialization.Should().Be("DevOps & SRE");
    }

    [Fact]
    public void GuardSpecialization_ThrowsWithParameterNameCapturedByCallerArgumentExpression()
    {
        // Act
        var customSpecialization = " ";
        var act = () => Instructor.Create(_validUserId, _validOrgId, customSpecialization);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("specialization");
    }

    [Fact]
    public void Constructor_Parameterless_IsProtected()
    {
        // Assert
        var ctor = typeof(Instructor).GetConstructor(
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
        var properties = typeof(Instructor).GetProperties(
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

        foreach (var prop in properties)
        {
            var setMethod = prop.GetSetMethod(nonPublic: true);
            setMethod.Should().NotBeNull($"Property {prop.Name} should have a setter");
            setMethod!.IsPrivate.Should().BeTrue($"Property {prop.Name} setter should be private");
        }
    }

    [Fact]
    public void LinkUser_WithValidUserId_UpdatesUserIdAndTimestamp()
    {
        // Arrange
        var instructor = Instructor.Create(_validUserId, _validOrgId, ValidSpecialization);
        var initialUpdatedAt = instructor.UpdatedAt;
        var newUserId = Guid.NewGuid();

        // Act
        instructor.LinkUser(newUserId);

        // Assert
        instructor.UserId.Should().Be(newUserId);
        instructor.UpdatedAt.Should().BeOnOrAfter(initialUpdatedAt);
    }

    [Fact]
    public void LinkUser_WithEmptyUserId_ThrowsArgumentException()
    {
        // Arrange
        var instructor = Instructor.Create(_validUserId, _validOrgId, ValidSpecialization);

        // Act
        var act = () => instructor.LinkUser(Guid.Empty);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*UserId cannot be empty*")
            .WithParameterName("newUserId");
    }
}
