using FluentAssertions;
using TrainHub.Domain.Constants;
using TrainHub.Domain.Models;

namespace TrainHub.UnitTests.Domain;

public class CourseGroupTests
{
    private readonly Guid _validOrgId = Guid.NewGuid();
    private readonly Guid _validCourseId = Guid.NewGuid();
    private readonly Guid _validInstructorId = Guid.NewGuid();
    private const string ValidName = "Group Alpha - Fall 2026";
    private const string ValidSchedule = "Mon & Wed 6:00 PM - 8:00 PM";
    private static readonly DateOnly ValidStartDate = new(2026, 10, 1);
    private static readonly DateOnly ValidEndDate = new(2026, 12, 1);

    [Fact]
    public void Create_WithValidParameters_ReturnsCourseGroupInstance()
    {
        // Act
        var group = CourseGroup.Create(
            _validOrgId,
            _validCourseId,
            ValidName,
            ValidStartDate,
            ValidEndDate,
            ValidSchedule,
            _validInstructorId,
            capacity: 25);

        // Assert
        group.Should().NotBeNull();
        group.Id.Should().NotBeEmpty();
        group.OrganizationId.Should().Be(_validOrgId);
        group.CourseId.Should().Be(_validCourseId);
        group.Name.Should().Be(ValidName);
        group.StartDate.Should().Be(ValidStartDate);
        group.EndDate.Should().Be(ValidEndDate);
        group.ScheduleDescription.Should().Be(ValidSchedule);
        group.InstructorId.Should().Be(_validInstructorId);
        group.Capacity.Should().Be(25);
        group.Status.Should().Be(CourseGroupConstants.DefaultStatus);
        group.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
        group.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void Create_WithDefaults_SetsExpectedDefaults()
    {
        // Act
        var group = CourseGroup.Create(_validOrgId, _validCourseId, ValidName, ValidStartDate, ValidEndDate);

        // Assert
        group.OrganizationId.Should().Be(_validOrgId);
        group.CourseId.Should().Be(_validCourseId);
        group.Name.Should().Be(ValidName);
        group.StartDate.Should().Be(ValidStartDate);
        group.EndDate.Should().Be(ValidEndDate);
        group.ScheduleDescription.Should().BeNull();
        group.InstructorId.Should().BeNull();
        group.Capacity.Should().Be(CourseGroupConstants.DefaultCapacity);
        group.Status.Should().Be(CourseGroupConstants.DefaultStatus);
    }

    [Fact]
    public void Create_WithEmptyOrganizationId_ThrowsArgumentException()
    {
        // Act
        var act = () => CourseGroup.Create(Guid.Empty, _validCourseId, ValidName, ValidStartDate, ValidEndDate);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*OrganizationId*");
    }

    [Fact]
    public void Create_WithEmptyCourseId_ThrowsArgumentException()
    {
        // Act
        var act = () => CourseGroup.Create(_validOrgId, Guid.Empty, ValidName, ValidStartDate, ValidEndDate);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*CourseId*");
    }

    [Fact]
    public void Create_WithEmptyInstructorId_ThrowsArgumentException()
    {
        // Act
        var act = () => CourseGroup.Create(
            _validOrgId,
            _validCourseId,
            ValidName,
            ValidStartDate,
            ValidEndDate,
            instructorId: Guid.Empty);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*InstructorId*");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("A")] // Too short (< 2)
    public void Create_WithInvalidName_ThrowsArgumentException(string? invalidName)
    {
        // Act
        var act = () => CourseGroup.Create(_validOrgId, _validCourseId, invalidName!, ValidStartDate, ValidEndDate);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*name*");
    }

    [Fact]
    public void Create_WithNameExceedingMaxLength_ThrowsArgumentException()
    {
        // Arrange
        var longName = new string('A', CourseGroupConstants.NameMaxLength + 1);

        // Act
        var act = () => CourseGroup.Create(_validOrgId, _validCourseId, longName, ValidStartDate, ValidEndDate);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*name*");
    }

    [Fact]
    public void Create_WithEndDateEarlierThanStartDate_ThrowsArgumentException()
    {
        // Arrange
        var startDate = new DateOnly(2026, 12, 1);
        var endDate = new DateOnly(2026, 10, 1);

        // Act
        var act = () => CourseGroup.Create(_validOrgId, _validCourseId, ValidName, startDate, endDate);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*EndDate*cannot be earlier than StartDate*");
    }

    [Fact]
    public void Create_WithScheduleDescriptionExceedingMaxLength_ThrowsArgumentException()
    {
        // Arrange
        var longSchedule = new string('x', CourseGroupConstants.ScheduleDescriptionMaxLength + 1);

        // Act
        var act = () => CourseGroup.Create(
            _validOrgId,
            _validCourseId,
            ValidName,
            ValidStartDate,
            ValidEndDate,
            scheduleDescription: longSchedule);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*ScheduleDescription*");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(1001)]
    public void Create_WithInvalidCapacity_ThrowsArgumentException(int invalidCapacity)
    {
        // Act
        var act = () => CourseGroup.Create(
            _validOrgId,
            _validCourseId,
            ValidName,
            ValidStartDate,
            ValidEndDate,
            capacity: invalidCapacity);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Capacity*");
    }

    [Fact]
    public void Create_TrimsWhitespaceFromStrings()
    {
        // Act
        var group = CourseGroup.Create(
            _validOrgId,
            _validCourseId,
            "   Batch 1   ",
            ValidStartDate,
            ValidEndDate,
            "   Mon 7 PM   ");

        // Assert
        group.Name.Should().Be("Batch 1");
        group.ScheduleDescription.Should().Be("Mon 7 PM");
    }

    [Fact]
    public void UpdateDetails_WithValidData_UpdatesPropertiesAndTimestamp()
    {
        // Arrange
        var group = CourseGroup.Create(_validOrgId, _validCourseId, ValidName, ValidStartDate, ValidEndDate);
        var initialUpdatedAt = group.UpdatedAt;

        var newStartDate = new DateOnly(2026, 11, 1);
        var newEndDate = new DateOnly(2026, 12, 15);
        var newInstructorId = Guid.NewGuid();

        // Act
        group.UpdateDetails(
            "Updated Batch 1",
            newStartDate,
            newEndDate,
            "Tue & Thu 6-8 PM",
            newInstructorId,
            45);

        // Assert
        group.Name.Should().Be("Updated Batch 1");
        group.StartDate.Should().Be(newStartDate);
        group.EndDate.Should().Be(newEndDate);
        group.ScheduleDescription.Should().Be("Tue & Thu 6-8 PM");
        group.InstructorId.Should().Be(newInstructorId);
        group.Capacity.Should().Be(45);
        group.UpdatedAt.Should().BeOnOrAfter(initialUpdatedAt);
    }

    [Fact]
    public void UpdateDetails_WithEmptyInstructorId_ThrowsArgumentException()
    {
        // Arrange
        var group = CourseGroup.Create(_validOrgId, _validCourseId, ValidName, ValidStartDate, ValidEndDate);

        // Act
        var act = () => group.UpdateDetails(
            ValidName,
            ValidStartDate,
            ValidEndDate,
            instructorId: Guid.Empty);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*InstructorId*");
    }

    [Fact]
    public void UpdateDetails_WithEndDateEarlierThanStartDate_ThrowsArgumentException()
    {
        // Arrange
        var group = CourseGroup.Create(_validOrgId, _validCourseId, ValidName, ValidStartDate, ValidEndDate);

        // Act
        var act = () => group.UpdateDetails(
            ValidName,
            new DateOnly(2026, 12, 1),
            new DateOnly(2026, 10, 1));

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*EndDate*cannot be earlier than StartDate*");
    }

    [Fact]
    public void GroupStatus_ReflectsStatusEnum()
    {
        // Act
        var group = CourseGroup.Create(
            _validOrgId, _validCourseId, ValidName, ValidStartDate, ValidEndDate,
            status: CourseGroupConstants.Statuses.Completed);

        // Assert
        group.Status.Should().Be(CourseGroupConstants.Statuses.Completed);
        group.GroupStatus.Should().Be(TrainHub.Domain.Enums.CourseGroupStatus.Completed);
    }

    [Fact]
    public void AssignInstructor_WithValidId_UpdatesInstructorIdAndTimestamp()
    {
        // Arrange
        var group = CourseGroup.Create(_validOrgId, _validCourseId, ValidName, ValidStartDate, ValidEndDate);
        var initialUpdatedAt = group.UpdatedAt;
        var instructorId = Guid.NewGuid();

        // Act
        group.AssignInstructor(instructorId);

        // Assert
        group.InstructorId.Should().Be(instructorId);
        group.UpdatedAt.Should().BeOnOrAfter(initialUpdatedAt);
    }

    [Fact]
    public void AssignInstructor_WithNull_UnassignsInstructor()
    {
        // Arrange
        var group = CourseGroup.Create(_validOrgId, _validCourseId, ValidName, ValidStartDate, ValidEndDate, instructorId: _validInstructorId);

        // Act
        group.AssignInstructor(null);

        // Assert
        group.InstructorId.Should().BeNull();
    }

    [Fact]
    public void UnassignInstructor_SetsInstructorIdToNull()
    {
        // Arrange
        var group = CourseGroup.Create(_validOrgId, _validCourseId, ValidName, ValidStartDate, ValidEndDate, instructorId: _validInstructorId);

        // Act
        group.UnassignInstructor();

        // Assert
        group.InstructorId.Should().BeNull();
    }

    [Fact]
    public void AssignInstructor_WithEmptyGuid_ThrowsArgumentException()
    {
        // Arrange
        var group = CourseGroup.Create(_validOrgId, _validCourseId, ValidName, ValidStartDate, ValidEndDate);

        // Act
        var act = () => group.AssignInstructor(Guid.Empty);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*InstructorId*");
    }

    [Fact]
    public void UpdateCapacity_WithValidCapacity_UpdatesCapacityAndTimestamp()
    {
        // Arrange
        var group = CourseGroup.Create(_validOrgId, _validCourseId, ValidName, ValidStartDate, ValidEndDate, capacity: 30);
        var initialUpdatedAt = group.UpdatedAt;

        // Act
        group.UpdateCapacity(50);

        // Assert
        group.Capacity.Should().Be(50);
        group.UpdatedAt.Should().BeOnOrAfter(initialUpdatedAt);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    [InlineData(1001)]
    public void UpdateCapacity_WithInvalidCapacity_ThrowsArgumentException(int invalidCapacity)
    {
        // Arrange
        var group = CourseGroup.Create(_validOrgId, _validCourseId, ValidName, ValidStartDate, ValidEndDate);

        // Act
        var act = () => group.UpdateCapacity(invalidCapacity);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Capacity*");
    }

    [Fact]
    public void UpdateStatus_WithValidStatus_UpdatesStatusAndTimestamp()
    {
        // Arrange
        var group = CourseGroup.Create(_validOrgId, _validCourseId, ValidName, ValidStartDate, ValidEndDate);
        var initialUpdatedAt = group.UpdatedAt;

        // Act
        group.UpdateStatus(CourseGroupConstants.Statuses.Completed);

        // Assert
        group.Status.Should().Be(CourseGroupConstants.Statuses.Completed);
        group.GroupStatus.Should().Be(TrainHub.Domain.Enums.CourseGroupStatus.Completed);
        group.UpdatedAt.Should().BeOnOrAfter(initialUpdatedAt);
    }

    [Fact]
    public void UpdateStatus_WithInvalidStatus_ThrowsArgumentException()
    {
        // Arrange
        var group = CourseGroup.Create(_validOrgId, _validCourseId, ValidName, ValidStartDate, ValidEndDate);

        // Act
        var act = () => group.UpdateStatus(99);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Status*");
    }

    [Fact]
    public void LifecycleMethods_UpdateStatusCorrectly()
    {
        // Arrange
        var group = CourseGroup.Create(
            _validOrgId, _validCourseId, ValidName, ValidStartDate, ValidEndDate,
            status: CourseGroupConstants.Statuses.Draft);

        // Act & Assert Activate
        group.Activate();
        group.Status.Should().Be(CourseGroupConstants.Statuses.Active);

        // Act & Assert Complete
        group.Complete();
        group.Status.Should().Be(CourseGroupConstants.Statuses.Completed);

        // Act & Assert Cancel
        group.Cancel();
        group.Status.Should().Be(CourseGroupConstants.Statuses.Cancelled);
    }
}
