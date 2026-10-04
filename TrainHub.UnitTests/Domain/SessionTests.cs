using FluentAssertions;
using TrainHub.Domain.Constants;
using TrainHub.Domain.Enums;
using TrainHub.Domain.Models;

namespace TrainHub.UnitTests.Domain;

public class SessionTests
{
    private readonly Guid _validOrgId = Guid.NewGuid();
    private readonly Guid _validCourseGroupId = Guid.NewGuid();
    private readonly Guid _validInstructorId = Guid.NewGuid();

    [Fact]
    public void Create_WithValidParameters_ReturnsSessionInstance()
    {
        // Arrange
        var startTime = new DateTime(2026, 10, 15, 10, 0, 0, DateTimeKind.Utc);
        var endTime = startTime.AddHours(2);

        // Act
        var session = Session.Create(
            _validOrgId,
            _validCourseGroupId,
            startTime,
            endTime,
            room: "Room 101",
            instructorId: _validInstructorId,
            status: SessionConstants.Statuses.Scheduled);

        // Assert
        session.Should().NotBeNull();
        session.Id.Should().NotBeEmpty();
        session.OrganizationId.Should().Be(_validOrgId);
        session.CourseGroupId.Should().Be(_validCourseGroupId);
        session.InstructorId.Should().Be(_validInstructorId);
        session.StartTime.Should().Be(startTime);
        session.EndTime.Should().Be(endTime);
        session.Room.Should().Be("Room 101");
        session.Status.Should().Be(SessionConstants.Statuses.Scheduled);
        session.SessionStatus.Should().Be(SessionStatus.Scheduled);
        session.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
        session.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void Create_WithDefaults_SetsExpectedDefaults()
    {
        // Arrange
        var startTime = DateTime.UtcNow.AddDays(1);
        var endTime = startTime.AddHours(1);

        // Act
        var session = Session.Create(_validOrgId, _validCourseGroupId, startTime, endTime);

        // Assert
        session.OrganizationId.Should().Be(_validOrgId);
        session.CourseGroupId.Should().Be(_validCourseGroupId);
        session.InstructorId.Should().BeNull();
        session.Room.Should().BeNull();
        session.Status.Should().Be(SessionConstants.DefaultStatus);
        session.SessionStatus.Should().Be(SessionStatus.Scheduled);
    }

    [Fact]
    public void Create_WithEmptyOrganizationId_ThrowsArgumentException()
    {
        var act = () => Session.Create(Guid.Empty, _validCourseGroupId, DateTime.UtcNow, DateTime.UtcNow.AddHours(1));

        act.Should().Throw<ArgumentException>()
            .WithMessage("*OrganizationId*");
    }

    [Fact]
    public void Create_WithEmptyCourseGroupId_ThrowsArgumentException()
    {
        var act = () => Session.Create(_validOrgId, Guid.Empty, DateTime.UtcNow, DateTime.UtcNow.AddHours(1));

        act.Should().Throw<ArgumentException>()
            .WithMessage("*CourseGroupId*");
    }

    [Fact]
    public void Create_WithEmptyInstructorId_ThrowsArgumentException()
    {
        var act = () => Session.Create(_validOrgId, _validCourseGroupId, DateTime.UtcNow, DateTime.UtcNow.AddHours(1), instructorId: Guid.Empty);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*InstructorId*");
    }

    [Fact]
    public void Create_WithEndTimeBeforeStartTime_ThrowsArgumentException()
    {
        var startTime = DateTime.UtcNow.AddHours(2);
        var endTime = startTime.AddHours(-1);

        var act = () => Session.Create(_validOrgId, _validCourseGroupId, startTime, endTime);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*EndTime*");
    }

    [Fact]
    public void Create_WithEndTimeEqualToStartTime_ThrowsArgumentException()
    {
        var time = DateTime.UtcNow.AddHours(2);

        var act = () => Session.Create(_validOrgId, _validCourseGroupId, time, time);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*EndTime*");
    }

    [Fact]
    public void Create_WithRoomExceedingMaxLength_ThrowsArgumentException()
    {
        var room = new string('A', SessionConstants.RoomMaxLength + 1);

        var act = () => Session.Create(_validOrgId, _validCourseGroupId, DateTime.UtcNow, DateTime.UtcNow.AddHours(1), room: room);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*Room*");
    }

    [Fact]
    public void Create_WithInvalidStatus_ThrowsArgumentException()
    {
        var act = () => Session.Create(_validOrgId, _validCourseGroupId, DateTime.UtcNow, DateTime.UtcNow.AddHours(1), status: 99);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*Status*");
    }

    [Fact]
    public void Create_TrimsAndNormalizesRoom()
    {
        var session = Session.Create(_validOrgId, _validCourseGroupId, DateTime.UtcNow, DateTime.UtcNow.AddHours(1), room: "  Lab 3  ");

        session.Room.Should().Be("Lab 3");

        var sessionWithEmptyRoom = Session.Create(_validOrgId, _validCourseGroupId, DateTime.UtcNow, DateTime.UtcNow.AddHours(1), room: "   ");
        sessionWithEmptyRoom.Room.Should().BeNull();
    }

    [Fact]
    public void UpdateDetails_WithValidData_UpdatesPropertiesAndTimestamp()
    {
        // Arrange
        var session = Session.Create(_validOrgId, _validCourseGroupId, DateTime.UtcNow, DateTime.UtcNow.AddHours(1));
        var newStart = DateTime.UtcNow.AddDays(2);
        var newEnd = newStart.AddHours(3);
        var newInstructor = Guid.NewGuid();

        // Act
        session.UpdateDetails(newStart, newEnd, "Lab 5", newInstructor, SessionConstants.Statuses.Completed);

        // Assert
        session.StartTime.Should().Be(newStart);
        session.EndTime.Should().Be(newEnd);
        session.Room.Should().Be("Lab 5");
        session.InstructorId.Should().Be(newInstructor);
        session.Status.Should().Be(SessionConstants.Statuses.Completed);
        session.SessionStatus.Should().Be(SessionStatus.Completed);
    }

    [Fact]
    public void UpdateDetails_WithInvalidTimeRange_ThrowsArgumentException()
    {
        var session = Session.Create(_validOrgId, _validCourseGroupId, DateTime.UtcNow, DateTime.UtcNow.AddHours(1));
        var newStart = DateTime.UtcNow.AddDays(2);
        var newEnd = newStart.AddHours(-1);

        var act = () => session.UpdateDetails(newStart, newEnd);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*EndTime*");
    }

    [Fact]
    public void Reschedule_WithValidTimes_UpdatesTimesAndTimestamp()
    {
        var session = Session.Create(_validOrgId, _validCourseGroupId, DateTime.UtcNow, DateTime.UtcNow.AddHours(1));
        var newStart = DateTime.UtcNow.AddDays(3);
        var newEnd = newStart.AddHours(2);

        session.Reschedule(newStart, newEnd, "Hall B");

        session.StartTime.Should().Be(newStart);
        session.EndTime.Should().Be(newEnd);
        session.Room.Should().Be("Hall B");
    }

    [Fact]
    public void AssignInstructor_WithValidInstructor_UpdatesInstructorId()
    {
        var session = Session.Create(_validOrgId, _validCourseGroupId, DateTime.UtcNow, DateTime.UtcNow.AddHours(1));
        var instructorId = Guid.NewGuid();

        session.AssignInstructor(instructorId);
        session.InstructorId.Should().Be(instructorId);

        session.UnassignInstructor();
        session.InstructorId.Should().BeNull();
    }

    [Fact]
    public void AssignInstructor_WithEmptyInstructorId_ThrowsArgumentException()
    {
        var session = Session.Create(_validOrgId, _validCourseGroupId, DateTime.UtcNow, DateTime.UtcNow.AddHours(1));

        var act = () => session.AssignInstructor(Guid.Empty);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*InstructorId*");
    }

    [Fact]
    public void UpdateStatus_WithValidStatus_UpdatesStatus()
    {
        var session = Session.Create(_validOrgId, _validCourseGroupId, DateTime.UtcNow, DateTime.UtcNow.AddHours(1));

        session.UpdateStatus(SessionConstants.Statuses.Completed);
        session.Status.Should().Be(SessionConstants.Statuses.Completed);

        session.UpdateStatus(SessionStatus.Cancelled);
        session.Status.Should().Be(SessionConstants.Statuses.Cancelled);

        session.Complete();
        session.Status.Should().Be(SessionConstants.Statuses.Completed);

        session.Cancel();
        session.Status.Should().Be(SessionConstants.Statuses.Cancelled);
    }

    [Fact]
    public void UpdateStatus_WithInvalidStatus_ThrowsArgumentException()
    {
        var session = Session.Create(_validOrgId, _validCourseGroupId, DateTime.UtcNow, DateTime.UtcNow.AddHours(1));

        var act = () => session.UpdateStatus(99);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*Status*");
    }
}
