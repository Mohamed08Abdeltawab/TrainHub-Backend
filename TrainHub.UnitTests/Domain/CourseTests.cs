using FluentAssertions;
using TrainHub.Domain.Constants;
using TrainHub.Domain.Models;

namespace TrainHub.UnitTests.Domain;

public class CourseTests
{
    private readonly Guid _validOrgId = Guid.NewGuid();
    private const string ValidName = "ASP.NET Core & Clean Architecture";
    private const string ValidDescription = "Comprehensive course covering DDD, Clean Architecture, and EF Core.";
    private const string ValidCategory = "Software Development";
    private const decimal ValidBasePrice = 199.99m;
    private const int ValidDurationInHours = 40;
    private const string ValidThumbnailUrl = "https://example.com/images/course.png";

    [Fact]
    public void Create_WithValidParameters_ReturnsCourseInstance()
    {
        // Act
        var course = Course.Create(
            _validOrgId,
            ValidName,
            ValidDescription,
            ValidCategory,
            ValidBasePrice,
            ValidDurationInHours,
            ValidThumbnailUrl,
            CourseConstants.Statuses.Active);

        // Assert
        course.Should().NotBeNull();
        course.Id.Should().NotBeEmpty();
        course.OrganizationId.Should().Be(_validOrgId);
        course.Name.Should().Be(ValidName);
        course.Description.Should().Be(ValidDescription);
        course.Category.Should().Be(ValidCategory);
        course.BasePrice.Should().Be(ValidBasePrice);
        course.DurationInHours.Should().Be(ValidDurationInHours);
        course.ThumbnailUrl.Should().Be(ValidThumbnailUrl);
        course.Status.Should().Be(CourseConstants.Statuses.Active);
        course.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
        course.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void Create_WithDefaults_SetsExpectedDefaults()
    {
        // Act
        var course = Course.Create(_validOrgId, ValidName);

        // Assert
        course.OrganizationId.Should().Be(_validOrgId);
        course.Name.Should().Be(ValidName);
        course.Description.Should().BeNull();
        course.Category.Should().BeNull();
        course.BasePrice.Should().Be(0);
        course.DurationInHours.Should().BeNull();
        course.ThumbnailUrl.Should().BeNull();
        course.Status.Should().Be(CourseConstants.Statuses.Active);
    }

    [Fact]
    public void Create_WithExplicitId_SetsProvidedId()
    {
        // Arrange
        var customId = Guid.NewGuid();

        // Act
        var course = Course.Create(
            _validOrgId,
            ValidName,
            id: customId);

        // Assert
        course.Id.Should().Be(customId);
    }

    [Fact]
    public void Create_WithEmptyOrganizationId_ThrowsArgumentException()
    {
        // Act
        var act = () => Course.Create(Guid.Empty, ValidName);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*OrganizationId cannot be empty*");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("   ")]
    public void Create_WithNullOrWhitespaceName_ThrowsArgumentException(string? invalidName)
    {
        // Act
        var act = () => Course.Create(_validOrgId, invalidName!);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Course name*is invalid*");
    }

    [Fact]
    public void Create_WithTooShortName_ThrowsArgumentException()
    {
        // Act
        var act = () => Course.Create(_validOrgId, "A");

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Course name*is invalid*");
    }

    [Fact]
    public void Create_WithTooLongName_ThrowsArgumentException()
    {
        // Arrange
        var tooLongName = new string('A', CourseConstants.NameMaxLength + 1);

        // Act
        var act = () => Course.Create(_validOrgId, tooLongName);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Course name*is invalid*");
    }

    [Fact]
    public void Create_WithTooLongDescription_ThrowsArgumentException()
    {
        // Arrange
        var tooLongDescription = new string('D', CourseConstants.DescriptionMaxLength + 1);

        // Act
        var act = () => Course.Create(_validOrgId, ValidName, description: tooLongDescription);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Description exceeds maximum length*");
    }

    [Fact]
    public void Create_WithTooShortCategory_ThrowsArgumentException()
    {
        // Act
        var act = () => Course.Create(_validOrgId, ValidName, category: "X");

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Category must be between*");
    }

    [Fact]
    public void Create_WithTooLongCategory_ThrowsArgumentException()
    {
        // Arrange
        var tooLongCategory = new string('C', CourseConstants.CategoryMaxLength + 1);

        // Act
        var act = () => Course.Create(_validOrgId, ValidName, category: tooLongCategory);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Category must be between*");
    }

    [Fact]
    public void Create_WithNegativeBasePrice_ThrowsArgumentException()
    {
        // Act
        var act = () => Course.Create(_validOrgId, ValidName, basePrice: -0.01m);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Base price must be between*");
    }

    [Fact]
    public void Create_WithExcessiveBasePrice_ThrowsArgumentException()
    {
        // Act
        var act = () => Course.Create(_validOrgId, ValidName, basePrice: CourseConstants.MaxBasePrice + 1);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Base price must be between*");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(10001)]
    public void Create_WithInvalidDuration_ThrowsArgumentException(int invalidDuration)
    {
        // Act
        var act = () => Course.Create(_validOrgId, ValidName, durationInHours: invalidDuration);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Duration in hours must be between*");
    }

    [Theory]
    [InlineData("not-a-valid-url")]
    [InlineData("ftp://example.com/file")]
    public void Create_WithInvalidThumbnailUrl_ThrowsArgumentException(string invalidUrl)
    {
        // Act
        var act = () => Course.Create(_validOrgId, ValidName, thumbnailUrl: invalidUrl);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Thumbnail URL is invalid*");
    }

    [Fact]
    public void Create_WithInvalidStatus_ThrowsArgumentException()
    {
        // Act
        var act = () => Course.Create(_validOrgId, ValidName, status: 99);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Status '99' is invalid*");
    }

    [Fact]
    public void Create_TrimsAndNormalizesFields()
    {
        // Act
        var course = Course.Create(
            _validOrgId,
            "  Advanced C#  ",
            "  Description with spaces  ",
            "  Programming  ",
            100m,
            20,
            "  https://example.com/img.jpg  ");

        // Assert
        course.Name.Should().Be("Advanced C#");
        course.Description.Should().Be("Description with spaces");
        course.Category.Should().Be("Programming");
        course.ThumbnailUrl.Should().Be("https://example.com/img.jpg");
    }

    [Fact]
    public void UpdateDetails_WithValidParameters_UpdatesFieldsAndTimestamp()
    {
        // Arrange
        var course = Course.Create(_validOrgId, ValidName);
        var initialUpdatedAt = course.UpdatedAt;

        // Act
        course.UpdateDetails(
            "Updated Course Name",
            "Updated Description",
            "Updated Category",
            299.99m,
            60,
            "https://example.com/new.png",
            CourseConstants.Statuses.Inactive);

        // Assert
        course.Name.Should().Be("Updated Course Name");
        course.Description.Should().Be("Updated Description");
        course.Category.Should().Be("Updated Category");
        course.BasePrice.Should().Be(299.99m);
        course.DurationInHours.Should().Be(60);
        course.ThumbnailUrl.Should().Be("https://example.com/new.png");
        course.Status.Should().Be(CourseConstants.Statuses.Inactive);
        course.UpdatedAt.Should().BeOnOrAfter(initialUpdatedAt);
    }

    [Fact]
    public void UpdateDetails_WithInvalidName_ThrowsArgumentException()
    {
        // Arrange
        var course = Course.Create(_validOrgId, ValidName);

        // Act
        var act = () => course.UpdateDetails("", null, null, 0, null, null);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Course name*is invalid*");
    }

    [Fact]
    public void UpdateStatus_WithValidStatus_UpdatesStatusAndTimestamp()
    {
        // Arrange
        var course = Course.Create(_validOrgId, ValidName);

        // Act
        course.UpdateStatus(CourseConstants.Statuses.Archived);

        // Assert
        course.Status.Should().Be(CourseConstants.Statuses.Archived);
    }

    [Fact]
    public void UpdateStatus_WithInvalidStatus_ThrowsArgumentException()
    {
        // Arrange
        var course = Course.Create(_validOrgId, ValidName);

        // Act
        var act = () => course.UpdateStatus(250);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Status '250' is invalid*");
    }

    [Fact]
    public void Activate_Deactivate_Archive_Methods_UpdateStatusCorrectly()
    {
        // Arrange
        var course = Course.Create(_validOrgId, ValidName, status: CourseConstants.Statuses.Draft);

        // Act & Assert
        course.Activate();
        course.Status.Should().Be(CourseConstants.Statuses.Active);

        course.Deactivate();
        course.Status.Should().Be(CourseConstants.Statuses.Inactive);

        course.Archive();
        course.Status.Should().Be(CourseConstants.Statuses.Archived);
    }
}
