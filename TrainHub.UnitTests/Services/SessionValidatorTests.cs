using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using TrainHub.Application.Common.Exceptions;
using TrainHub.Application.Features.Sessions.DTOs;
using TrainHub.Application.Services;
using TrainHub.Domain.Constants;
using TrainHub.Domain.Models;
using TrainHub.Infrastructure.Data;
using TrainHub.UnitTests.Common;

namespace TrainHub.UnitTests.Services;

public class SessionValidatorTests : IDisposable
{
    private readonly TrainHubDbContext _dbContext;
    private readonly Mock<ILogger<SessionValidator>> _loggerMock;
    private readonly SessionValidator _validator;
    private readonly Guid _organizationId = Guid.NewGuid();
    private readonly Course _course;
    private readonly CourseGroup _courseGroup;
    private readonly Instructor _instructor;

    public SessionValidatorTests()
    {
        _dbContext = TestHelpers.CreateInMemoryDbContext();
        _loggerMock = new Mock<ILogger<SessionValidator>>();
        _validator = new SessionValidator(_dbContext, _loggerMock.Object);

        // Seed Organization
        var org = new Organization
        {
            Id = _organizationId,
            Name = "Validator Org",
            Slug = "validator-org",
            IsActive = true
        };
        _dbContext.Organizations.Add(org);

        // Seed Course
        _course = Course.Create(_organizationId, "C# Fundamentals");
        _dbContext.Courses.Add(_course);

        // Seed CourseGroup
        _courseGroup = CourseGroup.Create(
            _organizationId,
            _course.Id,
            "Group 1",
            new DateOnly(2026, 10, 1),
            new DateOnly(2026, 12, 1));
        _dbContext.CourseGroups.Add(_courseGroup);

        // Seed User & Instructor
        var instructorUser = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            OrganizationId = _organizationId,
            UserName = "instructor@test.com",
            Email = "instructor@test.com",
            FirstName = "Alice",
            LastName = "Smith",
            IsActive = true
        };
        _dbContext.ApplicationUsers.Add(instructorUser);

        _instructor = Instructor.Create(instructorUser.Id, _organizationId, "Computer Science");
        _dbContext.Instructors.Add(_instructor);

        _dbContext.SaveChanges();
    }

    public void Dispose()
    {
        _dbContext.Database.EnsureDeleted();
        _dbContext.Dispose();
    }

    #region Query Validation Tests

    [Fact]
    public void ValidateQuery_WithValidParameters_DoesNotThrow()
    {
        var query = new SessionQueryParameters
        {
            PageNumber = 1,
            PageSize = 20,
            SortBy = "StartTime",
            SortDescending = true,
            FromDate = DateTime.UtcNow,
            ToDate = DateTime.UtcNow.AddDays(7)
        };

        var act = () => _validator.ValidateQuery(query);

        act.Should().NotThrow();
    }

    [Fact]
    public void ValidateQuery_WithInvalidStatus_ThrowsValidationException()
    {
        var query = new SessionQueryParameters { Status = 99 };

        var act = () => _validator.ValidateQuery(query);

        act.Should().Throw<ValidationException>()
            .Which.Errors.Should().ContainKey(nameof(query.Status));
    }

    [Fact]
    public void ValidateQuery_WithInvalidSortField_ThrowsValidationException()
    {
        var query = new SessionQueryParameters { SortBy = "InvalidSortColumn" };

        var act = () => _validator.ValidateQuery(query);

        act.Should().Throw<ValidationException>()
            .Which.Errors.Should().ContainKey(nameof(query.SortBy));
    }

    [Fact]
    public void ValidateQuery_WithToDateEarlierThanFromDate_ThrowsValidationException()
    {
        var query = new SessionQueryParameters
        {
            FromDate = DateTime.UtcNow.AddDays(5),
            ToDate = DateTime.UtcNow.AddDays(2)
        };

        var act = () => _validator.ValidateQuery(query);

        act.Should().Throw<ValidationException>()
            .Which.Errors.Should().ContainKey(nameof(query.ToDate));
    }

    #endregion

    #region Create Validation Tests

    [Fact]
    public async Task ValidateCreateAsync_WithValidDto_DoesNotThrow()
    {
        var dto = new CreateSessionDto
        {
            CourseGroupId = _courseGroup.Id,
            InstructorId = _instructor.Id,
            StartTime = DateTime.UtcNow.AddDays(1),
            EndTime = DateTime.UtcNow.AddDays(1).AddHours(2),
            Room = "Lab 101"
        };

        var act = () => _validator.ValidateCreateAsync(dto, _organizationId);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ValidateCreateAsync_WithEndTimeBeforeStartTime_ThrowsValidationException()
    {
        var dto = new CreateSessionDto
        {
            CourseGroupId = _courseGroup.Id,
            StartTime = DateTime.UtcNow.AddDays(1),
            EndTime = DateTime.UtcNow.AddDays(1).AddHours(-1)
        };

        var act = () => _validator.ValidateCreateAsync(dto, _organizationId);

        await act.Should().ThrowAsync<ValidationException>()
            .Where(ex => ex.Errors.ContainsKey(nameof(dto.EndTime)));
    }

    [Fact]
    public async Task ValidateCreateAsync_WhenOrganizationDoesNotExist_ThrowsNotFoundException()
    {
        var dto = new CreateSessionDto
        {
            CourseGroupId = _courseGroup.Id,
            StartTime = DateTime.UtcNow.AddDays(1),
            EndTime = DateTime.UtcNow.AddDays(1).AddHours(2)
        };

        var act = () => _validator.ValidateCreateAsync(dto, Guid.NewGuid());

        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("*Organization*");
    }

    [Fact]
    public async Task ValidateCreateAsync_WhenCourseGroupDoesNotExist_ThrowsNotFoundException()
    {
        var dto = new CreateSessionDto
        {
            CourseGroupId = Guid.NewGuid(),
            StartTime = DateTime.UtcNow.AddDays(1),
            EndTime = DateTime.UtcNow.AddDays(1).AddHours(2)
        };

        var act = () => _validator.ValidateCreateAsync(dto, _organizationId);

        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("*CourseGroup*");
    }

    [Fact]
    public async Task ValidateCreateAsync_WhenCourseGroupIsCancelled_ThrowsConflictException()
    {
        var cancelledGroup = CourseGroup.Create(
            _organizationId,
            _course.Id,
            "Cancelled Group",
            new DateOnly(2026, 10, 1),
            new DateOnly(2026, 12, 1));
        cancelledGroup.Cancel();
        _dbContext.CourseGroups.Add(cancelledGroup);
        await _dbContext.SaveChangesAsync();

        var dto = new CreateSessionDto
        {
            CourseGroupId = cancelledGroup.Id,
            StartTime = DateTime.UtcNow.AddDays(1),
            EndTime = DateTime.UtcNow.AddDays(1).AddHours(2)
        };

        var act = () => _validator.ValidateCreateAsync(dto, _organizationId);

        await act.Should().ThrowAsync<ConflictException>()
            .WithMessage("*cancelled*");
    }

    [Fact]
    public async Task ValidateCreateAsync_WhenInstructorDoesNotExist_ThrowsNotFoundException()
    {
        var dto = new CreateSessionDto
        {
            CourseGroupId = _courseGroup.Id,
            InstructorId = Guid.NewGuid(),
            StartTime = DateTime.UtcNow.AddDays(1),
            EndTime = DateTime.UtcNow.AddDays(1).AddHours(2)
        };

        var act = () => _validator.ValidateCreateAsync(dto, _organizationId);

        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("*Instructor*");
    }

    [Fact]
    public async Task ValidateCreateAsync_WhenInstructorUserIsInactive_ThrowsConflictException()
    {
        var inactiveUser = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            OrganizationId = _organizationId,
            UserName = "inactive@test.com",
            Email = "inactive@test.com",
            FirstName = "Bob",
            LastName = "Jones",
            IsActive = false
        };
        _dbContext.ApplicationUsers.Add(inactiveUser);

        var inactiveInstructor = Instructor.Create(inactiveUser.Id, _organizationId, "Math");
        _dbContext.Instructors.Add(inactiveInstructor);
        await _dbContext.SaveChangesAsync();

        var dto = new CreateSessionDto
        {
            CourseGroupId = _courseGroup.Id,
            InstructorId = inactiveInstructor.Id,
            StartTime = DateTime.UtcNow.AddDays(1),
            EndTime = DateTime.UtcNow.AddDays(1).AddHours(2)
        };

        var act = () => _validator.ValidateCreateAsync(dto, _organizationId);

        await act.Should().ThrowAsync<ConflictException>()
            .WithMessage("*inactive*");
    }

    #endregion

    #region Update Validation Tests

    [Fact]
    public async Task ValidateUpdateAsync_WhenSessionDoesNotExist_ThrowsNotFoundException()
    {
        var dto = new UpdateSessionDto
        {
            StartTime = DateTime.UtcNow.AddDays(1),
            EndTime = DateTime.UtcNow.AddDays(1).AddHours(2)
        };

        var act = () => _validator.ValidateUpdateAsync(Guid.NewGuid(), dto, _organizationId);

        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("*Session*");
    }

    [Fact]
    public async Task ValidateUpdateAsync_WhenSessionIsCancelled_ThrowsConflictException()
    {
        var session = Session.Create(_organizationId, _courseGroup.Id, DateTime.UtcNow.AddDays(1), DateTime.UtcNow.AddDays(1).AddHours(2));
        session.Cancel();
        _dbContext.Sessions.Add(session);
        await _dbContext.SaveChangesAsync();

        var dto = new UpdateSessionDto
        {
            StartTime = DateTime.UtcNow.AddDays(2),
            EndTime = DateTime.UtcNow.AddDays(2).AddHours(2)
        };

        var act = () => _validator.ValidateUpdateAsync(session.Id, dto, _organizationId);

        await act.Should().ThrowAsync<ConflictException>()
            .WithMessage("*cancelled*");
    }

    #endregion

    #region UpdateStatus Validation Tests

    [Fact]
    public async Task ValidateUpdateStatusAsync_WhenRevertingCompletedToScheduled_ThrowsConflictException()
    {
        var session = Session.Create(_organizationId, _courseGroup.Id, DateTime.UtcNow.AddDays(-2), DateTime.UtcNow.AddDays(-2).AddHours(2));
        session.Complete();
        _dbContext.Sessions.Add(session);
        await _dbContext.SaveChangesAsync();

        var act = () => _validator.ValidateUpdateStatusAsync(session.Id, SessionConstants.Statuses.Scheduled);

        await act.Should().ThrowAsync<ConflictException>()
            .WithMessage("*completed*");
    }

    [Fact]
    public async Task ValidateUpdateStatusAsync_WhenSessionIsCancelled_ThrowsConflictException()
    {
        var session = Session.Create(_organizationId, _courseGroup.Id, DateTime.UtcNow.AddDays(1), DateTime.UtcNow.AddDays(1).AddHours(2));
        session.Cancel();
        _dbContext.Sessions.Add(session);
        await _dbContext.SaveChangesAsync();

        var act = () => _validator.ValidateUpdateStatusAsync(session.Id, SessionConstants.Statuses.Scheduled);

        await act.Should().ThrowAsync<ConflictException>()
            .WithMessage("*cancelled*");
    }

    #endregion

    #region Delete Validation Tests

    [Fact]
    public async Task ValidateDeleteAsync_WhenSessionHasAttendanceRecords_ThrowsConflictException()
    {
        var session = Session.Create(_organizationId, _courseGroup.Id, DateTime.UtcNow.AddDays(1), DateTime.UtcNow.AddDays(1).AddHours(2));
        _dbContext.Sessions.Add(session);

        var studentUser = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            OrganizationId = _organizationId,
            UserName = "student@test.com",
            Email = "student@test.com",
            FirstName = "Student",
            LastName = "One",
            IsActive = true
        };
        _dbContext.ApplicationUsers.Add(studentUser);

        var student = Student.Create(studentUser.Id, _organizationId, "STU-001");
        _dbContext.Students.Add(student);

        var attendance = new AttendanceRecord
        {
            Id = Guid.NewGuid(),
            OrganizationId = _organizationId,
            SessionId = session.Id,
            StudentId = student.Id,
            Status = 1,
            MarkedAt = DateTime.UtcNow
        };
        _dbContext.AttendanceRecords.Add(attendance);
        await _dbContext.SaveChangesAsync();

        var act = () => _validator.ValidateDeleteAsync(session.Id);

        await act.Should().ThrowAsync<ConflictException>()
            .WithMessage("*associated attendance records*");
    }

    [Fact]
    public async Task ValidateDeleteAsync_WhenNoAttendanceRecords_DoesNotThrow()
    {
        var session = Session.Create(_organizationId, _courseGroup.Id, DateTime.UtcNow.AddDays(1), DateTime.UtcNow.AddDays(1).AddHours(2));
        _dbContext.Sessions.Add(session);
        await _dbContext.SaveChangesAsync();

        var act = () => _validator.ValidateDeleteAsync(session.Id);

        await act.Should().NotThrowAsync();
    }

    #endregion
}
