using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using TrainHub.Application.Common.Exceptions;
using TrainHub.Application.Features.Sessions.DTOs;
using TrainHub.Application.Interfaces;
using TrainHub.Application.Services;
using TrainHub.Domain.Constants;
using TrainHub.Domain.Models;
using TrainHub.Infrastructure.Data;
using TrainHub.UnitTests.Common;

namespace TrainHub.UnitTests.Services;

public class SessionServiceTests : IDisposable
{
    private readonly TrainHubDbContext _dbContext;
    private readonly Mock<ISessionValidator> _validatorMock;
    private readonly Mock<ICurrentOrganizationService> _currentOrgServiceMock;
    private readonly Mock<ILogger<SessionService>> _loggerMock;
    private readonly SessionService _service;
    private readonly Guid _organizationId = Guid.NewGuid();
    private readonly Course _course;
    private readonly CourseGroup _courseGroup;
    private readonly Instructor _instructor;

    public SessionServiceTests()
    {
        _dbContext = TestHelpers.CreateInMemoryDbContext();
        _validatorMock = new Mock<ISessionValidator>();
        _currentOrgServiceMock = new Mock<ICurrentOrganizationService>();
        _currentOrgServiceMock.Setup(s => s.OrganizationId).Returns(_organizationId);
        _loggerMock = new Mock<ILogger<SessionService>>();

        _service = new SessionService(
            _dbContext,
            _validatorMock.Object,
            _currentOrgServiceMock.Object,
            _loggerMock.Object);

        // Seed Organization
        var org = new Organization
        {
            Id = _organizationId,
            Name = "Service Test Org",
            Slug = "service-test-org",
            IsActive = true
        };
        _dbContext.Organizations.Add(org);

        // Seed User & Instructor
        var instructorUser = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            OrganizationId = _organizationId,
            UserName = "inst@test.com",
            Email = "inst@test.com",
            FirstName = "John",
            LastName = "Doe",
            IsActive = true
        };
        _dbContext.ApplicationUsers.Add(instructorUser);

        _instructor = Instructor.Create(instructorUser.Id, _organizationId, "Physics");
        _dbContext.Instructors.Add(_instructor);

        // Seed Course & CourseGroup
        _course = Course.Create(_organizationId, "Quantum Physics");
        _dbContext.Courses.Add(_course);

        _courseGroup = CourseGroup.Create(
            _organizationId,
            _course.Id,
            "Cohort 1",
            new DateOnly(2026, 10, 1),
            new DateOnly(2026, 12, 1),
            instructorId: _instructor.Id);
        _dbContext.CourseGroups.Add(_courseGroup);

        _dbContext.SaveChanges();
    }

    public void Dispose()
    {
        _dbContext.Database.EnsureDeleted();
        _dbContext.Dispose();
    }

    [Fact]
    public async Task GetPagedAsync_ReturnsPagedSessions()
    {
        // Arrange
        var session1 = Session.Create(_organizationId, _courseGroup.Id, DateTime.UtcNow.AddDays(1), DateTime.UtcNow.AddDays(1).AddHours(2), room: "Room A");
        var session2 = Session.Create(_organizationId, _courseGroup.Id, DateTime.UtcNow.AddDays(2), DateTime.UtcNow.AddDays(2).AddHours(2), room: "Room B");
        _dbContext.Sessions.AddRange(session1, session2);
        await _dbContext.SaveChangesAsync();

        var parameters = new SessionQueryParameters { PageNumber = 1, PageSize = 10 };

        // Act
        var result = await _service.GetPagedAsync(parameters, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.TotalCount.Should().Be(2);
        result.Items.Should().HaveCount(2);
        result.Items.First().CourseGroupName.Should().Be("Cohort 1");
        result.Items.First().CourseName.Should().Be("Quantum Physics");
    }

    [Fact]
    public async Task GetPagedAsync_FiltersByCourseGroupId()
    {
        // Arrange
        var anotherGroup = CourseGroup.Create(_organizationId, _course.Id, "Cohort 2", new DateOnly(2026, 10, 1), new DateOnly(2026, 12, 1));
        _dbContext.CourseGroups.Add(anotherGroup);

        var session1 = Session.Create(_organizationId, _courseGroup.Id, DateTime.UtcNow.AddDays(1), DateTime.UtcNow.AddDays(1).AddHours(2));
        var session2 = Session.Create(_organizationId, anotherGroup.Id, DateTime.UtcNow.AddDays(2), DateTime.UtcNow.AddDays(2).AddHours(2));
        _dbContext.Sessions.AddRange(session1, session2);
        await _dbContext.SaveChangesAsync();

        var parameters = new SessionQueryParameters { CourseGroupId = _courseGroup.Id };

        // Act
        var result = await _service.GetPagedAsync(parameters, CancellationToken.None);

        // Assert
        result.TotalCount.Should().Be(1);
        result.Items.Single().CourseGroupId.Should().Be(_courseGroup.Id);
    }

    [Fact]
    public async Task GetPagedAsync_FiltersByDateRange()
    {
        // Arrange
        var baseDate = new DateTime(2026, 11, 1, 10, 0, 0, DateTimeKind.Utc);
        var session1 = Session.Create(_organizationId, _courseGroup.Id, baseDate.AddDays(1), baseDate.AddDays(1).AddHours(2));
        var session2 = Session.Create(_organizationId, _courseGroup.Id, baseDate.AddDays(5), baseDate.AddDays(5).AddHours(2));
        var session3 = Session.Create(_organizationId, _courseGroup.Id, baseDate.AddDays(10), baseDate.AddDays(10).AddHours(2));
        _dbContext.Sessions.AddRange(session1, session2, session3);
        await _dbContext.SaveChangesAsync();

        var parameters = new SessionQueryParameters
        {
            FromDate = baseDate.AddDays(2),
            ToDate = baseDate.AddDays(7)
        };

        // Act
        var result = await _service.GetPagedAsync(parameters, CancellationToken.None);

        // Assert
        result.TotalCount.Should().Be(1);
        result.Items.Single().Id.Should().Be(session2.Id);
    }

    [Fact]
    public async Task GetByIdAsync_WhenSessionExists_ReturnsSessionDto()
    {
        // Arrange
        var session = Session.Create(
            _organizationId,
            _courseGroup.Id,
            DateTime.UtcNow.AddDays(1),
            DateTime.UtcNow.AddDays(1).AddHours(2),
            room: "Room 101",
            instructorId: _instructor.Id);
        _dbContext.Sessions.Add(session);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.GetByIdAsync(session.Id, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(session.Id);
        result.CourseGroupName.Should().Be("Cohort 1");
        result.CourseName.Should().Be("Quantum Physics");
        result.InstructorName.Should().Be("John Doe");
        result.Room.Should().Be("Room 101");
    }

    [Fact]
    public async Task GetByIdAsync_WhenSessionDoesNotExist_ThrowsNotFoundException()
    {
        var act = () => _service.GetByIdAsync(Guid.NewGuid(), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("*Session*");
    }

    [Fact]
    public async Task GetByCourseGroupIdAsync_WhenGroupExists_ReturnsSessionsList()
    {
        // Arrange
        var session1 = Session.Create(_organizationId, _courseGroup.Id, DateTime.UtcNow.AddDays(2), DateTime.UtcNow.AddDays(2).AddHours(2));
        var session2 = Session.Create(_organizationId, _courseGroup.Id, DateTime.UtcNow.AddDays(1), DateTime.UtcNow.AddDays(1).AddHours(2));
        _dbContext.Sessions.AddRange(session1, session2);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.GetByCourseGroupIdAsync(_courseGroup.Id, CancellationToken.None);

        // Assert
        result.Should().HaveCount(2);
        // Ordered chronologically
        result[0].StartTime.Should().BeBefore(result[1].StartTime);
    }

    [Fact]
    public async Task GetByCourseGroupIdAsync_WhenGroupDoesNotExist_ThrowsNotFoundException()
    {
        var act = () => _service.GetByCourseGroupIdAsync(Guid.NewGuid(), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("*CourseGroup*");
    }

    [Fact]
    public async Task CreateAsync_WithNullInstructor_InheritsFromCourseGroup()
    {
        // Arrange
        var dto = new CreateSessionDto
        {
            CourseGroupId = _courseGroup.Id,
            InstructorId = null, // Should inherit from course group
            StartTime = DateTime.UtcNow.AddDays(1),
            EndTime = DateTime.UtcNow.AddDays(1).AddHours(2),
            Room = "Lab A"
        };

        // Act
        var result = await _service.CreateAsync(dto, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.InstructorId.Should().Be(_instructor.Id);
        result.InstructorName.Should().Be("John Doe");
        result.Room.Should().Be("Lab A");
    }

    [Fact]
    public async Task CreateAsync_WhenNoOrgContext_ThrowsBadRequestException()
    {
        _currentOrgServiceMock.Setup(s => s.OrganizationId).Returns((Guid?)null);

        var dto = new CreateSessionDto
        {
            CourseGroupId = _courseGroup.Id,
            StartTime = DateTime.UtcNow.AddDays(1),
            EndTime = DateTime.UtcNow.AddDays(1).AddHours(2)
        };

        var act = () => _service.CreateAsync(dto, CancellationToken.None);

        await act.Should().ThrowAsync<BadRequestException>()
            .WithMessage("*Organization context is required*");
    }

    [Fact]
    public async Task UpdateAsync_WhenSessionExists_UpdatesAndReturnsDto()
    {
        // Arrange
        var session = Session.Create(_organizationId, _courseGroup.Id, DateTime.UtcNow.AddDays(1), DateTime.UtcNow.AddDays(1).AddHours(2));
        _dbContext.Sessions.Add(session);
        await _dbContext.SaveChangesAsync();

        var newStart = DateTime.UtcNow.AddDays(3);
        var newEnd = newStart.AddHours(2);
        var dto = new UpdateSessionDto
        {
            StartTime = newStart,
            EndTime = newEnd,
            Room = "Room 303",
            InstructorId = _instructor.Id,
            Status = SessionConstants.Statuses.Completed
        };

        // Act
        var result = await _service.UpdateAsync(session.Id, dto, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.StartTime.Should().Be(newStart);
        result.EndTime.Should().Be(newEnd);
        result.Room.Should().Be("Room 303");
        result.InstructorId.Should().Be(_instructor.Id);
        result.Status.Should().Be(SessionConstants.Statuses.Completed);
    }

    [Fact]
    public async Task UpdateStatusAsync_WhenSessionExists_UpdatesStatus()
    {
        // Arrange
        var session = Session.Create(_organizationId, _courseGroup.Id, DateTime.UtcNow.AddDays(1), DateTime.UtcNow.AddDays(1).AddHours(2));
        _dbContext.Sessions.Add(session);
        await _dbContext.SaveChangesAsync();

        var dto = new UpdateSessionStatusDto { Status = SessionConstants.Statuses.Cancelled };

        // Act
        var result = await _service.UpdateStatusAsync(session.Id, dto, CancellationToken.None);

        // Assert
        result.Status.Should().Be(SessionConstants.Statuses.Cancelled);
        result.StatusName.Should().Be("Cancelled");
    }

    [Fact]
    public async Task DeleteAsync_WhenSessionExists_RemovesFromDatabase()
    {
        // Arrange
        var session = Session.Create(_organizationId, _courseGroup.Id, DateTime.UtcNow.AddDays(1), DateTime.UtcNow.AddDays(1).AddHours(2));
        _dbContext.Sessions.Add(session);
        await _dbContext.SaveChangesAsync();

        // Act
        await _service.DeleteAsync(session.Id, CancellationToken.None);

        // Assert
        var deletedSession = await _dbContext.Sessions.FindAsync(session.Id);
        deletedSession.Should().BeNull();
    }
}
