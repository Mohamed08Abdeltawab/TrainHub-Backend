using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using TrainHub.Application.Common.Exceptions;
using TrainHub.Application.Features.Attendances.DTOs;
using TrainHub.Application.Interfaces;
using TrainHub.Application.Services;
using TrainHub.Domain.Constants;
using TrainHub.Domain.Models;
using TrainHub.Infrastructure.Data;
using TrainHub.UnitTests.Common;

namespace TrainHub.UnitTests.Services;

public class AttendanceServiceTests : IDisposable
{
    private readonly TrainHubDbContext _dbContext;
    private readonly Mock<IAttendanceValidator> _validatorMock;
    private readonly Mock<ICurrentOrganizationService> _currentOrgServiceMock;
    private readonly Mock<ILogger<AttendanceService>> _loggerMock;
    private readonly AttendanceService _service;
    private readonly Guid _organizationId = Guid.NewGuid();
    private readonly Course _course;
    private readonly CourseGroup _courseGroup;
    private readonly ApplicationUser _studentUser;
    private readonly Student _student;
    private readonly Session _session;

    public AttendanceServiceTests()
    {
        _dbContext = TestHelpers.CreateInMemoryDbContext();
        _validatorMock = new Mock<IAttendanceValidator>();
        _currentOrgServiceMock = new Mock<ICurrentOrganizationService>();
        _loggerMock = new Mock<ILogger<AttendanceService>>();

        _currentOrgServiceMock.Setup(s => s.OrganizationId).Returns(_organizationId);

        _service = new AttendanceService(
            _dbContext,
            _validatorMock.Object,
            _currentOrgServiceMock.Object,
            _loggerMock.Object);

        // Seed Organization
        var org = new Organization
        {
            Id = _organizationId,
            Name = "Service Org",
            Slug = "service-org",
            IsActive = true
        };
        _dbContext.Organizations.Add(org);

        // Seed Course & Group
        _course = Course.Create(_organizationId, "C# Mastery");
        _dbContext.Courses.Add(_course);

        _courseGroup = CourseGroup.Create(
            _organizationId,
            _course.Id,
            "Cohort A",
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-10)),
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)));
        _dbContext.CourseGroups.Add(_courseGroup);

        // Seed Student
        _studentUser = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            OrganizationId = _organizationId,
            UserName = "student@test.com",
            Email = "student@test.com",
            FirstName = "Alice",
            LastName = "Wonder",
            IsActive = true
        };
        _dbContext.ApplicationUsers.Add(_studentUser);

        _student = Student.Create(_studentUser.Id, _organizationId, "STU-001");
        _dbContext.Students.Add(_student);

        // Seed Session
        _session = Session.Create(
            _organizationId,
            _courseGroup.Id,
            DateTime.UtcNow.AddDays(1),
            DateTime.UtcNow.AddDays(1).AddHours(2),
            room: "Lab 1");
        _dbContext.Sessions.Add(_session);

        _dbContext.SaveChanges();
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }

    [Fact]
    public async Task GetPagedAsync_ReturnsPagedResultWithCorrectDtos()
    {
        // Arrange
        var record1 = AttendanceRecord.Create(_organizationId, _session.Id, _student.Id, AttendanceConstants.Statuses.Present);
        _dbContext.AttendanceRecords.Add(record1);
        await _dbContext.SaveChangesAsync();

        var query = new AttendanceQueryParameters { PageNumber = 1, PageSize = 10 };

        // Act
        var result = await _service.GetPagedAsync(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.TotalCount.Should().Be(1);
        result.Items.Should().HaveCount(1);
        result.Items[0].Id.Should().Be(record1.Id);
        result.Items[0].StudentCode.Should().Be("STU-001");
        result.Items[0].StudentName.Should().Be("Alice Wonder");
        result.Items[0].StatusName.Should().Be("Present");
    }

    [Fact]
    public async Task GetByIdAsync_WhenRecordExists_ReturnsDto()
    {
        // Arrange
        var record = AttendanceRecord.Create(
            _organizationId,
            _session.Id,
            _student.Id,
            status: AttendanceConstants.Statuses.Late,
            notes: "Traffic delay");
        _dbContext.AttendanceRecords.Add(record);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.GetByIdAsync(record.Id, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(record.Id);
        result.Status.Should().Be(AttendanceConstants.Statuses.Late);
        result.StatusName.Should().Be("Late");
        result.Notes.Should().Be("Traffic delay");
    }

    [Fact]
    public async Task GetByIdAsync_WhenRecordDoesNotExist_ThrowsNotFoundException()
    {
        // Act
        var act = () => _service.GetByIdAsync(Guid.NewGuid(), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("*AttendanceRecord*not found*");
    }

    [Fact]
    public async Task GetBySessionIdAsync_ReturnsAllRecordsForSession()
    {
        // Arrange
        var record = AttendanceRecord.Create(_organizationId, _session.Id, _student.Id, AttendanceConstants.Statuses.Present);
        _dbContext.AttendanceRecords.Add(record);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.GetBySessionIdAsync(_session.Id, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(1);
        result[0].SessionId.Should().Be(_session.Id);
        result[0].StudentId.Should().Be(_student.Id);
    }

    [Fact]
    public async Task GetBySessionIdAsync_WhenSessionDoesNotExist_ThrowsNotFoundException()
    {
        // Act
        var act = () => _service.GetBySessionIdAsync(Guid.NewGuid(), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("*Session*not found*");
    }

    [Fact]
    public async Task CreateAsync_WithValidDto_CreatesAndReturnsDto()
    {
        // Arrange
        var currentUserId = Guid.NewGuid();
        var dto = new CreateAttendanceDto
        {
            SessionId = _session.Id,
            StudentId = _student.Id,
            Status = AttendanceConstants.Statuses.Present,
            Notes = "All good"
        };

        // Act
        var result = await _service.CreateAsync(dto, currentUserId, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.SessionId.Should().Be(_session.Id);
        result.StudentId.Should().Be(_student.Id);
        result.Status.Should().Be(AttendanceConstants.Statuses.Present);
        result.Notes.Should().Be("All good");

        _validatorMock.Verify(v => v.ValidateCreateAsync(dto, _organizationId, currentUserId, It.IsAny<CancellationToken>()), Times.Once);
        _dbContext.AttendanceRecords.Should().Contain(a => a.Id == result.Id);
    }

    [Fact]
    public async Task CreateAsync_WithoutOrganizationContext_ThrowsBadRequestException()
    {
        // Arrange
        _currentOrgServiceMock.Setup(s => s.OrganizationId).Returns((Guid?)null);
        var dto = new CreateAttendanceDto { SessionId = _session.Id, StudentId = _student.Id };

        // Act
        var act = () => _service.CreateAsync(dto, null, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<BadRequestException>()
            .WithMessage("*Organization context is required*");
    }

    [Fact]
    public async Task UpdateAsync_WithValidDto_UpdatesAndReturnsDto()
    {
        // Arrange
        var record = AttendanceRecord.Create(_organizationId, _session.Id, _student.Id, AttendanceConstants.Statuses.Present);
        _dbContext.AttendanceRecords.Add(record);
        await _dbContext.SaveChangesAsync();

        var dto = new UpdateAttendanceDto
        {
            Status = AttendanceConstants.Statuses.Late,
            Notes = "Turned up late"
        };

        // Act
        var result = await _service.UpdateAsync(record.Id, dto, null, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(AttendanceConstants.Statuses.Late);
        result.Notes.Should().Be("Turned up late");

        var updatedInDb = await _dbContext.AttendanceRecords.FindAsync(record.Id);
        updatedInDb!.Status.Should().Be(AttendanceConstants.Statuses.Late);
        updatedInDb.Notes.Should().Be("Turned up late");
    }

    [Fact]
    public async Task UpdateStatusAsync_WithValidStatus_UpdatesStatus()
    {
        // Arrange
        var record = AttendanceRecord.Create(_organizationId, _session.Id, _student.Id, AttendanceConstants.Statuses.Present);
        _dbContext.AttendanceRecords.Add(record);
        await _dbContext.SaveChangesAsync();

        var dto = new UpdateAttendanceStatusDto { Status = AttendanceConstants.Statuses.Absent };

        // Act
        var result = await _service.UpdateStatusAsync(record.Id, dto, null, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(AttendanceConstants.Statuses.Absent);
        result.StatusName.Should().Be("Absent");

        var updatedInDb = await _dbContext.AttendanceRecords.FindAsync(record.Id);
        updatedInDb!.Status.Should().Be(AttendanceConstants.Statuses.Absent);
    }

    [Fact]
    public async Task DeleteAsync_WhenRecordExists_RemovesFromDatabase()
    {
        // Arrange
        var record = AttendanceRecord.Create(_organizationId, _session.Id, _student.Id);
        _dbContext.AttendanceRecords.Add(record);
        await _dbContext.SaveChangesAsync();

        // Act
        await _service.DeleteAsync(record.Id, null, CancellationToken.None);

        // Assert
        _dbContext.AttendanceRecords.Should().NotContain(a => a.Id == record.Id);
    }
}
