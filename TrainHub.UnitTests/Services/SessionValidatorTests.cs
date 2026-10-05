using FluentAssertions;
using Microsoft.AspNetCore.Identity;
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
    private readonly Mock<UserManager<ApplicationUser>> _userManagerMock;
    private readonly SessionValidator _validator;
    private readonly Guid _organizationId = Guid.NewGuid();
    private readonly Course _course;
    private readonly CourseGroup _courseGroup;
    private readonly ApplicationUser _instructorUser;
    private readonly Instructor _instructor;

    public SessionValidatorTests()
    {
        _dbContext = TestHelpers.CreateInMemoryDbContext();
        _loggerMock = new Mock<ILogger<SessionValidator>>();
        _userManagerMock = TestHelpers.CreateMockUserManager();
        _validator = new SessionValidator(_dbContext, _loggerMock.Object, _userManagerMock.Object);

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

        // Seed User & Instructor
        _instructorUser = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            OrganizationId = _organizationId,
            UserName = "instructor@test.com",
            Email = "instructor@test.com",
            FirstName = "Alice",
            LastName = "Smith",
            IsActive = true
        };
        _dbContext.ApplicationUsers.Add(_instructorUser);

        _instructor = Instructor.Create(_instructorUser.Id, _organizationId, "Computer Science");
        _dbContext.Instructors.Add(_instructor);

        // Seed CourseGroup assigned to instructor
        _courseGroup = CourseGroup.Create(
            _organizationId,
            _course.Id,
            "Group 1",
            new DateOnly(2026, 10, 1),
            new DateOnly(2026, 12, 1),
            instructorId: _instructor.Id);
        _dbContext.CourseGroups.Add(_courseGroup);

        _dbContext.SaveChanges();

        // Default user manager setup for the seeded instructor user
        _userManagerMock.Setup(m => m.FindByIdAsync(_instructorUser.Id.ToString()))
            .ReturnsAsync(_instructorUser);
        _userManagerMock.Setup(m => m.IsInRoleAsync(_instructorUser, AppRoles.SuperAdmin))
            .ReturnsAsync(false);
        _userManagerMock.Setup(m => m.IsInRoleAsync(_instructorUser, AppRoles.OrgAdmin))
            .ReturnsAsync(false);
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
        var validStart = _courseGroup.StartDate.ToDateTime(new TimeOnly(10, 0));
        var dto = new CreateSessionDto
        {
            CourseGroupId = _courseGroup.Id,
            InstructorId = _instructor.Id,
            StartTime = validStart,
            EndTime = validStart.AddHours(2),
            Room = "Lab 101"
        };

        var act = () => _validator.ValidateCreateAsync(dto, _organizationId);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ValidateCreateAsync_WithEndTimeBeforeStartTime_ThrowsValidationException()
    {
        var validStart = _courseGroup.StartDate.ToDateTime(new TimeOnly(10, 0));
        var dto = new CreateSessionDto
        {
            CourseGroupId = _courseGroup.Id,
            StartTime = validStart,
            EndTime = validStart.AddHours(-1)
        };

        var act = () => _validator.ValidateCreateAsync(dto, _organizationId);

        await act.Should().ThrowAsync<ValidationException>()
            .Where(ex => ex.Errors.ContainsKey(nameof(dto.EndTime)));
    }

    [Fact]
    public async Task ValidateCreateAsync_WhenOrganizationDoesNotExist_ThrowsNotFoundException()
    {
        var validStart = _courseGroup.StartDate.ToDateTime(new TimeOnly(10, 0));
        var dto = new CreateSessionDto
        {
            CourseGroupId = _courseGroup.Id,
            StartTime = validStart,
            EndTime = validStart.AddHours(2)
        };

        var act = () => _validator.ValidateCreateAsync(dto, Guid.NewGuid());

        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("*Organization*");
    }

    [Fact]
    public async Task ValidateCreateAsync_WhenCourseGroupDoesNotExist_ThrowsNotFoundException()
    {
        var validStart = _courseGroup.StartDate.ToDateTime(new TimeOnly(10, 0));
        var dto = new CreateSessionDto
        {
            CourseGroupId = Guid.NewGuid(),
            StartTime = validStart,
            EndTime = validStart.AddHours(2)
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

        var validStart = cancelledGroup.StartDate.ToDateTime(new TimeOnly(10, 0));
        var dto = new CreateSessionDto
        {
            CourseGroupId = cancelledGroup.Id,
            StartTime = validStart,
            EndTime = validStart.AddHours(2)
        };

        var act = () => _validator.ValidateCreateAsync(dto, _organizationId);

        await act.Should().ThrowAsync<ConflictException>()
            .WithMessage("*not active*");
    }

    [Fact]
    public async Task ValidateCreateAsync_WhenCourseIsArchived_ThrowsConflictException()
    {
        var archivedCourse = Course.Create(_organizationId, "Archived Course");
        archivedCourse.Archive();
        _dbContext.Courses.Add(archivedCourse);

        var groupInArchivedCourse = CourseGroup.Create(
            _organizationId,
            archivedCourse.Id,
            "Archived Course Group",
            new DateOnly(2026, 10, 1),
            new DateOnly(2026, 12, 1));
        _dbContext.CourseGroups.Add(groupInArchivedCourse);
        await _dbContext.SaveChangesAsync();

        var validStart = groupInArchivedCourse.StartDate.ToDateTime(new TimeOnly(10, 0));
        var dto = new CreateSessionDto
        {
            CourseGroupId = groupInArchivedCourse.Id,
            StartTime = validStart,
            EndTime = validStart.AddHours(2)
        };

        var act = () => _validator.ValidateCreateAsync(dto, _organizationId);

        await act.Should().ThrowAsync<ConflictException>()
            .WithMessage("*archived*");
    }

    [Fact]
    public async Task ValidateCreateAsync_WhenDateBeforeCourseGroupStartDate_ThrowsValidationException()
    {
        var beforeStart = _courseGroup.StartDate.AddDays(-1).ToDateTime(new TimeOnly(10, 0));
        var dto = new CreateSessionDto
        {
            CourseGroupId = _courseGroup.Id,
            StartTime = beforeStart,
            EndTime = beforeStart.AddHours(2)
        };

        var act = () => _validator.ValidateCreateAsync(dto, _organizationId);

        await act.Should().ThrowAsync<ValidationException>()
            .Where(ex => ex.Errors.ContainsKey(nameof(dto.StartTime)));
    }

    [Fact]
    public async Task ValidateCreateAsync_WhenDateAfterCourseGroupEndDate_ThrowsValidationException()
    {
        var afterEnd = _courseGroup.EndDate.AddDays(1).ToDateTime(new TimeOnly(10, 0));
        var dto = new CreateSessionDto
        {
            CourseGroupId = _courseGroup.Id,
            StartTime = afterEnd,
            EndTime = afterEnd.AddHours(2)
        };

        var act = () => _validator.ValidateCreateAsync(dto, _organizationId);

        await act.Should().ThrowAsync<ValidationException>()
            .Where(ex => ex.Errors.ContainsKey(nameof(dto.StartTime)));
    }

    [Fact]
    public async Task ValidateCreateAsync_WhenInstructorDoesNotExist_ThrowsNotFoundException()
    {
        var validStart = _courseGroup.StartDate.ToDateTime(new TimeOnly(10, 0));
        var dto = new CreateSessionDto
        {
            CourseGroupId = _courseGroup.Id,
            InstructorId = Guid.NewGuid(),
            StartTime = validStart,
            EndTime = validStart.AddHours(2)
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

        var validStart = _courseGroup.StartDate.ToDateTime(new TimeOnly(10, 0));
        var dto = new CreateSessionDto
        {
            CourseGroupId = _courseGroup.Id,
            InstructorId = inactiveInstructor.Id,
            StartTime = validStart,
            EndTime = validStart.AddHours(2)
        };

        var act = () => _validator.ValidateCreateAsync(dto, _organizationId);

        await act.Should().ThrowAsync<ConflictException>()
            .WithMessage("*inactive*");
    }

    #endregion

    #region Reschedule Validation Tests

    [Fact]
    public async Task ValidateRescheduleAsync_WithValidDto_DoesNotThrow()
    {
        var initialStart = _courseGroup.StartDate.ToDateTime(new TimeOnly(10, 0));
        var session = Session.Create(_organizationId, _courseGroup.Id, initialStart, initialStart.AddHours(2));
        _dbContext.Sessions.Add(session);
        await _dbContext.SaveChangesAsync();

        var newStart = _courseGroup.StartDate.AddDays(5).ToDateTime(new TimeOnly(14, 0));
        var dto = new RescheduleSessionDto
        {
            StartTime = newStart,
            EndTime = newStart.AddHours(2),
            Room = "Auditorium 1"
        };

        var act = () => _validator.ValidateRescheduleAsync(session.Id, dto, _organizationId);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ValidateRescheduleAsync_WhenSessionDoesNotExist_ThrowsNotFoundException()
    {
        var validStart = _courseGroup.StartDate.ToDateTime(new TimeOnly(10, 0));
        var dto = new RescheduleSessionDto
        {
            StartTime = validStart,
            EndTime = validStart.AddHours(2)
        };

        var act = () => _validator.ValidateRescheduleAsync(Guid.NewGuid(), dto, _organizationId);

        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("*Session*");
    }

    [Fact]
    public async Task ValidateRescheduleAsync_WhenSessionIsCancelled_ThrowsConflictException()
    {
        var initialStart = _courseGroup.StartDate.ToDateTime(new TimeOnly(10, 0));
        var session = Session.Create(_organizationId, _courseGroup.Id, initialStart, initialStart.AddHours(2));
        session.Cancel();
        _dbContext.Sessions.Add(session);
        await _dbContext.SaveChangesAsync();

        var newStart = _courseGroup.StartDate.AddDays(2).ToDateTime(new TimeOnly(10, 0));
        var dto = new RescheduleSessionDto
        {
            StartTime = newStart,
            EndTime = newStart.AddHours(2)
        };

        var act = () => _validator.ValidateRescheduleAsync(session.Id, dto, _organizationId);

        await act.Should().ThrowAsync<ConflictException>()
            .WithMessage("*reschedule a cancelled session*");
    }

    [Fact]
    public async Task ValidateRescheduleAsync_WhenSessionIsCompleted_ThrowsConflictException()
    {
        var initialStart = _courseGroup.StartDate.ToDateTime(new TimeOnly(10, 0));
        var session = Session.Create(_organizationId, _courseGroup.Id, initialStart, initialStart.AddHours(2));
        session.Complete();
        _dbContext.Sessions.Add(session);
        await _dbContext.SaveChangesAsync();

        var newStart = _courseGroup.StartDate.AddDays(2).ToDateTime(new TimeOnly(10, 0));
        var dto = new RescheduleSessionDto
        {
            StartTime = newStart,
            EndTime = newStart.AddHours(2)
        };

        var act = () => _validator.ValidateRescheduleAsync(session.Id, dto, _organizationId);

        await act.Should().ThrowAsync<ConflictException>()
            .WithMessage("*reschedule a completed session*");
    }

    [Fact]
    public async Task ValidateRescheduleAsync_WhenDateOutsideCourseGroupBoundary_ThrowsValidationException()
    {
        var initialStart = _courseGroup.StartDate.ToDateTime(new TimeOnly(10, 0));
        var session = Session.Create(_organizationId, _courseGroup.Id, initialStart, initialStart.AddHours(2));
        _dbContext.Sessions.Add(session);
        await _dbContext.SaveChangesAsync();

        var outsideDate = _courseGroup.EndDate.AddDays(1).ToDateTime(new TimeOnly(10, 0));
        var dto = new RescheduleSessionDto
        {
            StartTime = outsideDate,
            EndTime = outsideDate.AddHours(2)
        };

        var act = () => _validator.ValidateRescheduleAsync(session.Id, dto, _organizationId);

        await act.Should().ThrowAsync<ValidationException>()
            .Where(ex => ex.Errors.ContainsKey(nameof(dto.StartTime)));
    }

    #endregion

    #region Update Validation Tests

    [Fact]
    public async Task ValidateUpdateAsync_WhenSessionDoesNotExist_ThrowsNotFoundException()
    {
        var validStart = _courseGroup.StartDate.ToDateTime(new TimeOnly(10, 0));
        var dto = new UpdateSessionDto
        {
            StartTime = validStart,
            EndTime = validStart.AddHours(2)
        };

        var act = () => _validator.ValidateUpdateAsync(Guid.NewGuid(), dto, _organizationId);

        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("*Session*");
    }

    [Fact]
    public async Task ValidateUpdateAsync_WhenSessionIsCancelled_ThrowsConflictException()
    {
        var validStart = _courseGroup.StartDate.ToDateTime(new TimeOnly(10, 0));
        var session = Session.Create(_organizationId, _courseGroup.Id, validStart, validStart.AddHours(2));
        session.Cancel();
        _dbContext.Sessions.Add(session);
        await _dbContext.SaveChangesAsync();

        var dto = new UpdateSessionDto
        {
            StartTime = validStart.AddDays(1),
            EndTime = validStart.AddDays(1).AddHours(2)
        };

        var act = () => _validator.ValidateUpdateAsync(session.Id, dto, _organizationId);

        await act.Should().ThrowAsync<ConflictException>()
            .WithMessage("*cancelled*");
    }

    [Fact]
    public async Task ValidateUpdateAsync_WhenSessionIsCompleted_ThrowsConflictException()
    {
        var validStart = _courseGroup.StartDate.ToDateTime(new TimeOnly(10, 0));
        var session = Session.Create(_organizationId, _courseGroup.Id, validStart, validStart.AddHours(2));
        session.Complete();
        _dbContext.Sessions.Add(session);
        await _dbContext.SaveChangesAsync();

        var dto = new UpdateSessionDto
        {
            StartTime = validStart.AddDays(1),
            EndTime = validStart.AddDays(1).AddHours(2)
        };

        var act = () => _validator.ValidateUpdateAsync(session.Id, dto, _organizationId);

        await act.Should().ThrowAsync<ConflictException>()
            .WithMessage("*completed*");
    }

    [Fact]
    public async Task ValidateUpdateAsync_WhenMarkingFutureSessionCompleted_ThrowsConflictException()
    {
        var futureDate = DateTime.UtcNow.AddDays(10);
        var session = Session.Create(_organizationId, _courseGroup.Id, futureDate, futureDate.AddHours(2));
        _dbContext.Sessions.Add(session);
        await _dbContext.SaveChangesAsync();

        var dto = new UpdateSessionDto
        {
            StartTime = futureDate,
            EndTime = futureDate.AddHours(2),
            Status = SessionConstants.Statuses.Completed
        };

        var act = () => _validator.ValidateUpdateAsync(session.Id, dto, _organizationId);

        await act.Should().ThrowAsync<ConflictException>()
            .WithMessage("*future session as completed*");
    }

    [Fact]
    public async Task ValidateUpdateAsync_WhenDateOutsideCourseGroupBoundary_ThrowsValidationException()
    {
        var initialStart = _courseGroup.StartDate.ToDateTime(new TimeOnly(10, 0));
        var session = Session.Create(_organizationId, _courseGroup.Id, initialStart, initialStart.AddHours(2));
        _dbContext.Sessions.Add(session);
        await _dbContext.SaveChangesAsync();

        var outsideDate = _courseGroup.EndDate.AddDays(2).ToDateTime(new TimeOnly(10, 0));
        var dto = new UpdateSessionDto
        {
            StartTime = outsideDate,
            EndTime = outsideDate.AddHours(2)
        };

        var act = () => _validator.ValidateUpdateAsync(session.Id, dto, _organizationId);

        await act.Should().ThrowAsync<ValidationException>()
            .Where(ex => ex.Errors.ContainsKey(nameof(dto.StartTime)));
    }

    #endregion

    #region UpdateStatus Validation Tests

    [Fact]
    public async Task ValidateUpdateStatusAsync_WhenRevertingCompletedToScheduled_ThrowsConflictException()
    {
        var pastStart = DateTime.UtcNow.AddDays(-2);
        var session = Session.Create(_organizationId, _courseGroup.Id, pastStart, pastStart.AddHours(2));
        session.Complete();
        _dbContext.Sessions.Add(session);
        await _dbContext.SaveChangesAsync();

        var act = () => _validator.ValidateUpdateStatusAsync(session.Id, SessionConstants.Statuses.Scheduled, _organizationId);

        await act.Should().ThrowAsync<ConflictException>()
            .WithMessage("*completed*");
    }

    [Fact]
    public async Task ValidateUpdateStatusAsync_WhenSessionIsCancelled_ThrowsConflictException()
    {
        var validStart = _courseGroup.StartDate.ToDateTime(new TimeOnly(10, 0));
        var session = Session.Create(_organizationId, _courseGroup.Id, validStart, validStart.AddHours(2));
        session.Cancel();
        _dbContext.Sessions.Add(session);
        await _dbContext.SaveChangesAsync();

        var act = () => _validator.ValidateUpdateStatusAsync(session.Id, SessionConstants.Statuses.Scheduled, _organizationId);

        await act.Should().ThrowAsync<ConflictException>()
            .WithMessage("*cancelled*");
    }

    [Fact]
    public async Task ValidateUpdateStatusAsync_WhenMarkingFutureSessionCompleted_ThrowsConflictException()
    {
        var futureDate = DateTime.UtcNow.AddDays(5);
        var session = Session.Create(_organizationId, _courseGroup.Id, futureDate, futureDate.AddHours(2));
        _dbContext.Sessions.Add(session);
        await _dbContext.SaveChangesAsync();

        var act = () => _validator.ValidateUpdateStatusAsync(session.Id, SessionConstants.Statuses.Completed, _organizationId);

        await act.Should().ThrowAsync<ConflictException>()
            .WithMessage("*future session as completed*");
    }

    #endregion

    #region Delete Validation Tests

    [Fact]
    public async Task ValidateDeleteAsync_WhenSessionHasAttendanceRecords_ThrowsConflictException()
    {
        var validStart = _courseGroup.StartDate.ToDateTime(new TimeOnly(10, 0));
        var session = Session.Create(_organizationId, _courseGroup.Id, validStart, validStart.AddHours(2));
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

        var attendance = AttendanceRecord.Create(
            _organizationId,
            session.Id,
            student.Id,
            status: 1);
        _dbContext.AttendanceRecords.Add(attendance);
        await _dbContext.SaveChangesAsync();

        var act = () => _validator.ValidateDeleteAsync(session.Id, _organizationId);

        await act.Should().ThrowAsync<ConflictException>()
            .WithMessage("*associated attendance records*");
    }

    [Fact]
    public async Task ValidateDeleteAsync_WhenNoAttendanceRecords_DoesNotThrow()
    {
        var validStart = _courseGroup.StartDate.ToDateTime(new TimeOnly(10, 0));
        var session = Session.Create(_organizationId, _courseGroup.Id, validStart, validStart.AddHours(2));
        _dbContext.Sessions.Add(session);
        await _dbContext.SaveChangesAsync();

        var act = () => _validator.ValidateDeleteAsync(session.Id, _organizationId);

        await act.Should().NotThrowAsync();
    }

    #endregion

    #region Instructor Authorization Tests

    [Fact]
    public async Task ValidateCreateAsync_WhenInstructorAssignedToCourseGroup_AllowsScheduling()
    {
        var validStart = _courseGroup.StartDate.ToDateTime(new TimeOnly(10, 0));
        var dto = new CreateSessionDto
        {
            CourseGroupId = _courseGroup.Id,
            InstructorId = _instructor.Id,
            StartTime = validStart,
            EndTime = validStart.AddHours(2)
        };

        // Caller is the instructor assigned to _courseGroup
        var act = () => _validator.ValidateCreateAsync(dto, _organizationId, _instructorUser.Id);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ValidateCreateAsync_WhenInstructorNotAssignedToCourseGroup_ThrowsForbiddenAccessException()
    {
        // Seed another instructor
        var otherUser = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            OrganizationId = _organizationId,
            UserName = "other@test.com",
            Email = "other@test.com",
            FirstName = "Other",
            LastName = "Instructor",
            IsActive = true
        };
        _dbContext.ApplicationUsers.Add(otherUser);

        var otherInstructor = Instructor.Create(otherUser.Id, _organizationId, "Chemistry");
        _dbContext.Instructors.Add(otherInstructor);
        await _dbContext.SaveChangesAsync();

        _userManagerMock.Setup(m => m.FindByIdAsync(otherUser.Id.ToString()))
            .ReturnsAsync(otherUser);
        _userManagerMock.Setup(m => m.IsInRoleAsync(otherUser, AppRoles.SuperAdmin))
            .ReturnsAsync(false);
        _userManagerMock.Setup(m => m.IsInRoleAsync(otherUser, AppRoles.OrgAdmin))
            .ReturnsAsync(false);

        var validStart = _courseGroup.StartDate.ToDateTime(new TimeOnly(10, 0));
        var dto = new CreateSessionDto
        {
            CourseGroupId = _courseGroup.Id, // assigned to _instructor, not otherInstructor
            InstructorId = null,
            StartTime = validStart,
            EndTime = validStart.AddHours(2)
        };

        var act = () => _validator.ValidateCreateAsync(dto, _organizationId, otherUser.Id);

        await act.Should().ThrowAsync<ForbiddenAccessException>()
            .WithMessage("*not authorized to schedule sessions for this course group*");
    }

    [Fact]
    public async Task ValidateCreateAsync_WhenInstructorAttemptsToAssignAnotherInstructor_ThrowsForbiddenAccessException()
    {
        // Seed another instructor
        var otherUser = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            OrganizationId = _organizationId,
            UserName = "other2@test.com",
            Email = "other2@test.com",
            FirstName = "Other",
            LastName = "Two",
            IsActive = true
        };
        _dbContext.ApplicationUsers.Add(otherUser);

        var otherInstructor = Instructor.Create(otherUser.Id, _organizationId, "Biology");
        _dbContext.Instructors.Add(otherInstructor);
        await _dbContext.SaveChangesAsync();

        var validStart = _courseGroup.StartDate.ToDateTime(new TimeOnly(10, 0));
        var dto = new CreateSessionDto
        {
            CourseGroupId = _courseGroup.Id, // taught by _instructor
            InstructorId = otherInstructor.Id, // trying to assign to otherInstructor
            StartTime = validStart,
            EndTime = validStart.AddHours(2)
        };

        var act = () => _validator.ValidateCreateAsync(dto, _organizationId, _instructorUser.Id);

        await act.Should().ThrowAsync<ForbiddenAccessException>()
            .WithMessage("*Instructors cannot assign sessions to other instructors*");
    }

    [Fact]
    public async Task ValidateUpdateAsync_WhenInstructorDoesNotOwnSession_ThrowsForbiddenAccessException()
    {
        // Seed another instructor
        var otherUser = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            OrganizationId = _organizationId,
            UserName = "other3@test.com",
            Email = "other3@test.com",
            FirstName = "Other",
            LastName = "Three",
            IsActive = true
        };
        _dbContext.ApplicationUsers.Add(otherUser);

        var otherInstructor = Instructor.Create(otherUser.Id, _organizationId, "Math");
        _dbContext.Instructors.Add(otherInstructor);

        var validStart = _courseGroup.StartDate.ToDateTime(new TimeOnly(10, 0));
        var session = Session.Create(_organizationId, _courseGroup.Id, validStart, validStart.AddHours(2), instructorId: _instructor.Id);
        _dbContext.Sessions.Add(session);
        await _dbContext.SaveChangesAsync();

        _userManagerMock.Setup(m => m.FindByIdAsync(otherUser.Id.ToString()))
            .ReturnsAsync(otherUser);
        _userManagerMock.Setup(m => m.IsInRoleAsync(otherUser, AppRoles.SuperAdmin))
            .ReturnsAsync(false);
        _userManagerMock.Setup(m => m.IsInRoleAsync(otherUser, AppRoles.OrgAdmin))
            .ReturnsAsync(false);

        var dto = new UpdateSessionDto
        {
            StartTime = validStart.AddDays(1),
            EndTime = validStart.AddDays(1).AddHours(2)
        };

        var act = () => _validator.ValidateUpdateAsync(session.Id, dto, _organizationId, otherUser.Id);

        await act.Should().ThrowAsync<ForbiddenAccessException>()
            .WithMessage("*not authorized to manage this session*");
    }

    [Fact]
    public async Task ValidateUpdateAsync_WhenInstructorAttemptsToReassignSession_ThrowsForbiddenAccessException()
    {
        // Seed another instructor
        var otherUser = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            OrganizationId = _organizationId,
            UserName = "other4@test.com",
            Email = "other4@test.com",
            FirstName = "Other",
            LastName = "Four",
            IsActive = true
        };
        _dbContext.ApplicationUsers.Add(otherUser);

        var otherInstructor = Instructor.Create(otherUser.Id, _organizationId, "Math");
        _dbContext.Instructors.Add(otherInstructor);

        var validStart = _courseGroup.StartDate.ToDateTime(new TimeOnly(10, 0));
        var session = Session.Create(_organizationId, _courseGroup.Id, validStart, validStart.AddHours(2), instructorId: _instructor.Id);
        _dbContext.Sessions.Add(session);
        await _dbContext.SaveChangesAsync();

        var dto = new UpdateSessionDto
        {
            StartTime = validStart.AddDays(1),
            EndTime = validStart.AddDays(1).AddHours(2),
            InstructorId = otherInstructor.Id // attempting reassign
        };

        var act = () => _validator.ValidateUpdateAsync(session.Id, dto, _organizationId, _instructorUser.Id);

        await act.Should().ThrowAsync<ForbiddenAccessException>()
            .WithMessage("*Instructors cannot reassign sessions to other instructors*");
    }

    [Fact]
    public async Task ValidateRescheduleAsync_WhenInstructorOwnsSession_AllowsRescheduling()
    {
        var validStart = _courseGroup.StartDate.ToDateTime(new TimeOnly(10, 0));
        var session = Session.Create(_organizationId, _courseGroup.Id, validStart, validStart.AddHours(2), instructorId: _instructor.Id);
        _dbContext.Sessions.Add(session);
        await _dbContext.SaveChangesAsync();

        var newStart = _courseGroup.StartDate.AddDays(3).ToDateTime(new TimeOnly(14, 0));
        var dto = new RescheduleSessionDto
        {
            StartTime = newStart,
            EndTime = newStart.AddHours(2),
            Room = "Room 404"
        };

        var act = () => _validator.ValidateRescheduleAsync(session.Id, dto, _organizationId, _instructorUser.Id);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ValidateCreateAsync_WhenOrgAdminUser_BypassesInstructorOwnershipCheck()
    {
        var adminUser = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            OrganizationId = _organizationId,
            UserName = "admin@test.com",
            Email = "admin@test.com",
            FirstName = "Org",
            LastName = "Admin",
            IsActive = true
        };
        _dbContext.ApplicationUsers.Add(adminUser);
        await _dbContext.SaveChangesAsync();

        _userManagerMock.Setup(m => m.FindByIdAsync(adminUser.Id.ToString()))
            .ReturnsAsync(adminUser);
        _userManagerMock.Setup(m => m.IsInRoleAsync(adminUser, AppRoles.OrgAdmin))
            .ReturnsAsync(true);
        _userManagerMock.Setup(m => m.IsInRoleAsync(adminUser, AppRoles.SuperAdmin))
            .ReturnsAsync(false);

        var validStart = _courseGroup.StartDate.ToDateTime(new TimeOnly(10, 0));
        var dto = new CreateSessionDto
        {
            CourseGroupId = _courseGroup.Id,
            InstructorId = _instructor.Id,
            StartTime = validStart,
            EndTime = validStart.AddHours(2)
        };

        // OrgAdmin is not an instructor, but can manage anything within the organization
        var act = () => _validator.ValidateCreateAsync(dto, _organizationId, adminUser.Id);

        await act.Should().NotThrowAsync();
    }

    #endregion
}
