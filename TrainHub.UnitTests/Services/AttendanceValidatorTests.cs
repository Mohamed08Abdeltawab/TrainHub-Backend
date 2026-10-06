using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Moq;
using TrainHub.Application.Common.Exceptions;
using TrainHub.Application.Features.Attendances.DTOs;
using TrainHub.Application.Services;
using TrainHub.Domain.Constants;
using TrainHub.Domain.Models;
using TrainHub.Infrastructure.Data;
using TrainHub.UnitTests.Common;

namespace TrainHub.UnitTests.Services;

public class AttendanceValidatorTests : IDisposable
{
    private readonly TrainHubDbContext _dbContext;
    private readonly Mock<ILogger<AttendanceValidator>> _loggerMock;
    private readonly Mock<UserManager<ApplicationUser>> _userManagerMock;
    private readonly AttendanceValidator _validator;
    private readonly Guid _organizationId = Guid.NewGuid();
    private readonly Course _course;
    private readonly CourseGroup _courseGroup;
    private readonly ApplicationUser _instructorUser;
    private readonly Instructor _instructor;
    private readonly ApplicationUser _studentUser;
    private readonly Student _student;
    private readonly Enrollment _enrollment;
    private readonly Session _session;

    public AttendanceValidatorTests()
    {
        _dbContext = TestHelpers.CreateInMemoryDbContext();
        _loggerMock = new Mock<ILogger<AttendanceValidator>>();
        _userManagerMock = TestHelpers.CreateMockUserManager();
        _validator = new AttendanceValidator(_dbContext, _loggerMock.Object, _userManagerMock.Object);

        // Seed Organization
        var org = new Organization
        {
            Id = _organizationId,
            Name = "Attendance Org",
            Slug = "attendance-org",
            IsActive = true
        };
        _dbContext.Organizations.Add(org);

        // Seed Course
        _course = Course.Create(_organizationId, "C# Mastery");
        _dbContext.Courses.Add(_course);

        // Seed Instructor
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

        _instructor = Instructor.Create(_instructorUser.Id, _organizationId, "Backend Specialist");
        _dbContext.Instructors.Add(_instructor);

        // Seed CourseGroup
        _courseGroup = CourseGroup.Create(
            _organizationId,
            _course.Id,
            "Cohort 2026-A",
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-10)),
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(50)),
            instructorId: _instructor.Id);
        _dbContext.CourseGroups.Add(_courseGroup);

        // Seed Student
        _studentUser = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            OrganizationId = _organizationId,
            UserName = "student@test.com",
            Email = "student@test.com",
            FirstName = "Bob",
            LastName = "Jones",
            IsActive = true
        };
        _dbContext.ApplicationUsers.Add(_studentUser);

        _student = Student.Create(_studentUser.Id, _organizationId, "STU-100");
        _dbContext.Students.Add(_student);

        // Seed Enrollment
        _enrollment = Enrollment.Create(_organizationId, _courseGroup.Id, _student.Id);
        _dbContext.Enrollments.Add(_enrollment);

        // Seed Session
        _session = Session.Create(
            _organizationId,
            _courseGroup.Id,
            DateTime.UtcNow.AddDays(1),
            DateTime.UtcNow.AddDays(1).AddHours(2),
            room: "Room 101",
            instructorId: _instructor.Id);
        _dbContext.Sessions.Add(_session);

        _dbContext.SaveChanges();
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }

    #region Query Validation Tests

    [Fact]
    public void ValidateQuery_WithValidParameters_DoesNotThrow()
    {
        // Arrange
        var query = new AttendanceQueryParameters
        {
            PageNumber = 1,
            PageSize = 20,
            FromDate = DateTime.UtcNow.AddDays(-1),
            ToDate = DateTime.UtcNow.AddDays(1),
            SortBy = "MarkedAt",
            SortDescending = true
        };

        // Act & Assert
        var act = () => _validator.ValidateQuery(query);
        act.Should().NotThrow();
    }

    [Fact]
    public void ValidateQuery_WithNullQuery_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => _validator.ValidateQuery(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ValidateQuery_WithInvalidDateRange_ThrowsValidationException()
    {
        // Arrange
        var query = new AttendanceQueryParameters
        {
            FromDate = DateTime.UtcNow.AddDays(2),
            ToDate = DateTime.UtcNow.AddDays(1)
        };

        // Act & Assert
        var act = () => _validator.ValidateQuery(query);
        act.Should().Throw<ValidationException>()
            .Which.Errors.Should().ContainKey(nameof(query.FromDate));
    }

    [Fact]
    public void ValidateQuery_WithInvalidSortBy_ThrowsValidationException()
    {
        // Arrange
        var query = new AttendanceQueryParameters { SortBy = "InvalidSortColumn" };

        // Act & Assert
        var act = () => _validator.ValidateQuery(query);
        act.Should().Throw<ValidationException>()
            .Which.Errors.Should().ContainKey(nameof(query.SortBy));
    }

    [Fact]
    public void ValidateQuery_WithInvalidStatus_ThrowsValidationException()
    {
        // Arrange
        var query = new AttendanceQueryParameters { Status = 99 };

        // Act & Assert
        var act = () => _validator.ValidateQuery(query);
        act.Should().Throw<ValidationException>()
            .Which.Errors.Should().ContainKey(nameof(query.Status));
    }

    #endregion

    #region Create Validation Tests

    [Fact]
    public async Task ValidateCreateAsync_WithValidDataAsOrgStaff_DoesNotThrow()
    {
        // Arrange
        var staffUserId = Guid.NewGuid();
        var staffUser = new ApplicationUser { Id = staffUserId, OrganizationId = _organizationId, UserName = "admin@test.com" };
        _userManagerMock.Setup(m => m.FindByIdAsync(staffUserId.ToString())).ReturnsAsync(staffUser);
        _userManagerMock.Setup(m => m.IsInRoleAsync(staffUser, AppRoles.OrgAdmin)).ReturnsAsync(true);

        var dto = new CreateAttendanceDto
        {
            SessionId = _session.Id,
            StudentId = _student.Id,
            Status = AttendanceConstants.Statuses.Present,
            Notes = "Present on time"
        };

        // Act & Assert
        var act = () => _validator.ValidateCreateAsync(dto, _organizationId, staffUserId);
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ValidateCreateAsync_WithValidDataAsAssignedInstructor_DoesNotThrow()
    {
        // Arrange
        _userManagerMock.Setup(m => m.FindByIdAsync(_instructorUser.Id.ToString())).ReturnsAsync(_instructorUser);
        _userManagerMock.Setup(m => m.IsInRoleAsync(_instructorUser, It.IsAny<string>())).ReturnsAsync(false);

        var dto = new CreateAttendanceDto
        {
            SessionId = _session.Id,
            StudentId = _student.Id,
            Status = AttendanceConstants.Statuses.Present
        };

        // Act & Assert
        var act = () => _validator.ValidateCreateAsync(dto, _organizationId, _instructorUser.Id);
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ValidateCreateAsync_WithInvalidStatus_ThrowsValidationException()
    {
        // Arrange
        var dto = new CreateAttendanceDto
        {
            SessionId = _session.Id,
            StudentId = _student.Id,
            Status = 99
        };

        // Act & Assert
        var act = () => _validator.ValidateCreateAsync(dto, _organizationId);
        await act.Should().ThrowAsync<ValidationException>()
            .Where(ex => ex.Errors.ContainsKey(nameof(dto.Status)));
    }

    [Fact]
    public async Task ValidateCreateAsync_WithExcessivelyLongNotes_ThrowsValidationException()
    {
        // Arrange
        var dto = new CreateAttendanceDto
        {
            SessionId = _session.Id,
            StudentId = _student.Id,
            Status = AttendanceConstants.Statuses.Present,
            Notes = new string('N', AttendanceConstants.NotesMaxLength + 1)
        };

        // Act & Assert
        var act = () => _validator.ValidateCreateAsync(dto, _organizationId);
        await act.Should().ThrowAsync<ValidationException>()
            .Where(ex => ex.Errors.ContainsKey(nameof(dto.Notes)));
    }

    [Fact]
    public async Task ValidateCreateAsync_WhenSessionDoesNotExist_ThrowsNotFoundException()
    {
        // Arrange
        var dto = new CreateAttendanceDto
        {
            SessionId = Guid.NewGuid(),
            StudentId = _student.Id,
            Status = AttendanceConstants.Statuses.Present
        };

        // Act & Assert
        var act = () => _validator.ValidateCreateAsync(dto, _organizationId);
        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("*Session*not found*");
    }

    [Fact]
    public async Task ValidateCreateAsync_WhenSessionBelongsToAnotherOrg_ThrowsNotFoundException()
    {
        // Arrange
        var otherOrgId = Guid.NewGuid();
        var dto = new CreateAttendanceDto
        {
            SessionId = _session.Id,
            StudentId = _student.Id,
            Status = AttendanceConstants.Statuses.Present
        };

        // Act & Assert
        var act = () => _validator.ValidateCreateAsync(dto, otherOrgId);
        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("*Session*not found*");
    }

    [Fact]
    public async Task ValidateCreateAsync_WhenSessionIsCancelled_ThrowsConflictException()
    {
        // Arrange
        _session.Cancel();
        await _dbContext.SaveChangesAsync();

        var dto = new CreateAttendanceDto
        {
            SessionId = _session.Id,
            StudentId = _student.Id,
            Status = AttendanceConstants.Statuses.Present
        };

        // Act & Assert
        var act = () => _validator.ValidateCreateAsync(dto, _organizationId);
        await act.Should().ThrowAsync<ConflictException>()
            .WithMessage("*cancelled session*");
    }

    [Fact]
    public async Task ValidateCreateAsync_WhenSessionIsDraft_ThrowsConflictException()
    {
        // Arrange
        var draftSession = Session.Create(
            _organizationId,
            _courseGroup.Id,
            DateTime.UtcNow.AddDays(2),
            DateTime.UtcNow.AddDays(2).AddHours(2),
            status: SessionConstants.Statuses.Draft);
        _dbContext.Sessions.Add(draftSession);
        await _dbContext.SaveChangesAsync();

        var dto = new CreateAttendanceDto
        {
            SessionId = draftSession.Id,
            StudentId = _student.Id,
            Status = AttendanceConstants.Statuses.Present
        };

        // Act & Assert
        var act = () => _validator.ValidateCreateAsync(dto, _organizationId);
        await act.Should().ThrowAsync<ConflictException>()
            .WithMessage("*draft session*");
    }

    [Fact]
    public async Task ValidateCreateAsync_WhenCourseGroupIsCancelled_ThrowsConflictException()
    {
        // Arrange
        _courseGroup.Cancel();
        await _dbContext.SaveChangesAsync();

        var dto = new CreateAttendanceDto
        {
            SessionId = _session.Id,
            StudentId = _student.Id,
            Status = AttendanceConstants.Statuses.Present
        };

        // Act & Assert
        var act = () => _validator.ValidateCreateAsync(dto, _organizationId);
        await act.Should().ThrowAsync<ConflictException>()
            .WithMessage("*cancelled course group*");
    }

    [Fact]
    public async Task ValidateCreateAsync_WhenStudentUserIsInactive_ThrowsConflictException()
    {
        // Arrange
        _studentUser.IsActive = false;
        await _dbContext.SaveChangesAsync();

        var dto = new CreateAttendanceDto
        {
            SessionId = _session.Id,
            StudentId = _student.Id,
            Status = AttendanceConstants.Statuses.Present
        };

        // Act & Assert
        var act = () => _validator.ValidateCreateAsync(dto, _organizationId);
        await act.Should().ThrowAsync<ConflictException>()
            .WithMessage("*inactive*");
    }

    [Fact]
    public async Task ValidateCreateAsync_WhenStudentEnrollmentIsPending_ThrowsConflictException()
    {
        // Arrange
        _enrollment.UpdateDetails(status: EnrollmentConstants.Statuses.Pending);
        await _dbContext.SaveChangesAsync();

        var dto = new CreateAttendanceDto
        {
            SessionId = _session.Id,
            StudentId = _student.Id,
            Status = AttendanceConstants.Statuses.Present
        };

        // Act & Assert
        var act = () => _validator.ValidateCreateAsync(dto, _organizationId);
        await act.Should().ThrowAsync<ConflictException>()
            .WithMessage("*enrollment is pending*");
    }

    [Fact]
    public async Task ValidateCreateAsync_WhenStudentDoesNotExist_ThrowsNotFoundException()
    {
        // Arrange
        var dto = new CreateAttendanceDto
        {
            SessionId = _session.Id,
            StudentId = Guid.NewGuid(),
            Status = AttendanceConstants.Statuses.Present
        };

        // Act & Assert
        var act = () => _validator.ValidateCreateAsync(dto, _organizationId);
        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("*Student*not found*");
    }

    [Fact]
    public async Task ValidateCreateAsync_WhenStudentIsNotEnrolled_ThrowsConflictException()
    {
        // Arrange
        var otherUser = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            OrganizationId = _organizationId,
            UserName = "other@test.com",
            Email = "other@test.com",
            FirstName = "Other",
            LastName = "Student",
            IsActive = true
        };
        _dbContext.ApplicationUsers.Add(otherUser);

        var otherStudent = Student.Create(otherUser.Id, _organizationId, "STU-999");
        _dbContext.Students.Add(otherStudent);
        await _dbContext.SaveChangesAsync();

        var dto = new CreateAttendanceDto
        {
            SessionId = _session.Id,
            StudentId = otherStudent.Id,
            Status = AttendanceConstants.Statuses.Present
        };

        // Act & Assert
        var act = () => _validator.ValidateCreateAsync(dto, _organizationId);
        await act.Should().ThrowAsync<ConflictException>()
            .WithMessage("*not enrolled in the course group*");
    }

    [Fact]
    public async Task ValidateCreateAsync_WhenStudentEnrollmentIsCancelled_ThrowsConflictException()
    {
        // Arrange
        _enrollment.Cancel();
        await _dbContext.SaveChangesAsync();

        var dto = new CreateAttendanceDto
        {
            SessionId = _session.Id,
            StudentId = _student.Id,
            Status = AttendanceConstants.Statuses.Present
        };

        // Act & Assert
        var act = () => _validator.ValidateCreateAsync(dto, _organizationId);
        await act.Should().ThrowAsync<ConflictException>()
            .WithMessage("*enrollment is cancelled*");
    }

    [Fact]
    public async Task ValidateCreateAsync_WhenDuplicateAttendanceRecordExists_ThrowsConflictException()
    {
        // Arrange
        var existing = AttendanceRecord.Create(
            _organizationId,
            _session.Id,
            _student.Id,
            AttendanceConstants.Statuses.Present);
        _dbContext.AttendanceRecords.Add(existing);
        await _dbContext.SaveChangesAsync();

        var dto = new CreateAttendanceDto
        {
            SessionId = _session.Id,
            StudentId = _student.Id,
            Status = AttendanceConstants.Statuses.Present
        };

        // Act & Assert
        var act = () => _validator.ValidateCreateAsync(dto, _organizationId);
        await act.Should().ThrowAsync<ConflictException>()
            .WithMessage("*already exists for this student in this session*");
    }

    [Fact]
    public async Task ValidateCreateAsync_WhenInstructorNotAssigned_ThrowsForbiddenAccessException()
    {
        // Arrange
        var otherInstructorUser = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            OrganizationId = _organizationId,
            UserName = "unassigned@test.com",
            Email = "unassigned@test.com",
            FirstName = "Unassigned",
            LastName = "Instructor"
        };
        _dbContext.ApplicationUsers.Add(otherInstructorUser);

        var otherInstructor = Instructor.Create(otherInstructorUser.Id, _organizationId, "Other");
        _dbContext.Instructors.Add(otherInstructor);
        await _dbContext.SaveChangesAsync();

        _userManagerMock.Setup(m => m.FindByIdAsync(otherInstructorUser.Id.ToString())).ReturnsAsync(otherInstructorUser);
        _userManagerMock.Setup(m => m.IsInRoleAsync(otherInstructorUser, It.IsAny<string>())).ReturnsAsync(false);

        var dto = new CreateAttendanceDto
        {
            SessionId = _session.Id,
            StudentId = _student.Id,
            Status = AttendanceConstants.Statuses.Present
        };

        // Act & Assert
        var act = () => _validator.ValidateCreateAsync(dto, _organizationId, otherInstructorUser.Id);
        await act.Should().ThrowAsync<ForbiddenAccessException>()
            .WithMessage("*not authorized to manage attendance for this session*");
    }

    #endregion

    #region Update Validation Tests

    [Fact]
    public async Task ValidateUpdateAsync_WithValidData_DoesNotThrow()
    {
        // Arrange
        var record = AttendanceRecord.Create(_organizationId, _session.Id, _student.Id);
        _dbContext.AttendanceRecords.Add(record);
        await _dbContext.SaveChangesAsync();

        var dto = new UpdateAttendanceDto
        {
            Status = AttendanceConstants.Statuses.Late,
            Notes = "Updated notes"
        };

        // Act & Assert
        var act = () => _validator.ValidateUpdateAsync(record.Id, dto, _organizationId);
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ValidateUpdateAsync_WhenRecordDoesNotExist_ThrowsNotFoundException()
    {
        // Arrange
        var dto = new UpdateAttendanceDto { Status = AttendanceConstants.Statuses.Present };

        // Act & Assert
        var act = () => _validator.ValidateUpdateAsync(Guid.NewGuid(), dto, _organizationId);
        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("*AttendanceRecord*not found*");
    }

    [Fact]
    public async Task ValidateUpdateAsync_WhenSessionIsCancelled_ThrowsConflictException()
    {
        // Arrange
        var record = AttendanceRecord.Create(_organizationId, _session.Id, _student.Id);
        _dbContext.AttendanceRecords.Add(record);

        _session.Cancel();
        await _dbContext.SaveChangesAsync();

        var dto = new UpdateAttendanceDto { Status = AttendanceConstants.Statuses.Late };

        // Act & Assert
        var act = () => _validator.ValidateUpdateAsync(record.Id, dto, _organizationId);
        await act.Should().ThrowAsync<ConflictException>()
            .WithMessage("*cancelled session*");
    }

    [Fact]
    public async Task ValidateUpdateAsync_WhenCourseGroupIsCancelled_ThrowsConflictException()
    {
        // Arrange
        var record = AttendanceRecord.Create(_organizationId, _session.Id, _student.Id);
        _dbContext.AttendanceRecords.Add(record);

        _courseGroup.Cancel();
        await _dbContext.SaveChangesAsync();

        var dto = new UpdateAttendanceDto { Status = AttendanceConstants.Statuses.Late };

        // Act & Assert
        var act = () => _validator.ValidateUpdateAsync(record.Id, dto, _organizationId);
        await act.Should().ThrowAsync<ConflictException>()
            .WithMessage("*cancelled course group*");
    }

    [Fact]
    public async Task ValidateUpdateAsync_WhenEnrollmentIsCancelled_ThrowsConflictException()
    {
        // Arrange
        var record = AttendanceRecord.Create(_organizationId, _session.Id, _student.Id);
        _dbContext.AttendanceRecords.Add(record);

        _enrollment.Cancel();
        await _dbContext.SaveChangesAsync();

        var dto = new UpdateAttendanceDto { Status = AttendanceConstants.Statuses.Late };

        // Act & Assert
        var act = () => _validator.ValidateUpdateAsync(record.Id, dto, _organizationId);
        await act.Should().ThrowAsync<ConflictException>()
            .WithMessage("*enrollment is cancelled*");
    }

    #endregion

    #region Update Status Validation Tests

    [Fact]
    public async Task ValidateUpdateStatusAsync_WithValidStatus_DoesNotThrow()
    {
        // Arrange
        var record = AttendanceRecord.Create(_organizationId, _session.Id, _student.Id);
        _dbContext.AttendanceRecords.Add(record);
        await _dbContext.SaveChangesAsync();

        // Act & Assert
        var act = () => _validator.ValidateUpdateStatusAsync(record.Id, AttendanceConstants.Statuses.Excused, _organizationId);
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ValidateUpdateStatusAsync_WithInvalidStatus_ThrowsValidationException()
    {
        // Act & Assert
        var act = () => _validator.ValidateUpdateStatusAsync(Guid.NewGuid(), 99, _organizationId);
        await act.Should().ThrowAsync<ValidationException>()
            .Where(ex => ex.Errors.ContainsKey("status"));
    }

    [Fact]
    public async Task ValidateUpdateStatusAsync_WhenCourseGroupIsCancelled_ThrowsConflictException()
    {
        // Arrange
        var record = AttendanceRecord.Create(_organizationId, _session.Id, _student.Id);
        _dbContext.AttendanceRecords.Add(record);

        _courseGroup.Cancel();
        await _dbContext.SaveChangesAsync();

        // Act & Assert
        var act = () => _validator.ValidateUpdateStatusAsync(record.Id, AttendanceConstants.Statuses.Excused, _organizationId);
        await act.Should().ThrowAsync<ConflictException>()
            .WithMessage("*cancelled course group*");
    }

    [Fact]
    public async Task ValidateUpdateStatusAsync_WhenEnrollmentIsCancelled_ThrowsConflictException()
    {
        // Arrange
        var record = AttendanceRecord.Create(_organizationId, _session.Id, _student.Id);
        _dbContext.AttendanceRecords.Add(record);

        _enrollment.Cancel();
        await _dbContext.SaveChangesAsync();

        // Act & Assert
        var act = () => _validator.ValidateUpdateStatusAsync(record.Id, AttendanceConstants.Statuses.Excused, _organizationId);
        await act.Should().ThrowAsync<ConflictException>()
            .WithMessage("*enrollment is cancelled*");
    }

    #endregion

    #region Delete Validation Tests

    [Fact]
    public async Task ValidateDeleteAsync_WithValidRecord_DoesNotThrow()
    {
        // Arrange
        var record = AttendanceRecord.Create(_organizationId, _session.Id, _student.Id);
        _dbContext.AttendanceRecords.Add(record);
        await _dbContext.SaveChangesAsync();

        // Act & Assert
        var act = () => _validator.ValidateDeleteAsync(record.Id, _organizationId);
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ValidateDeleteAsync_WhenRecordDoesNotExist_ThrowsNotFoundException()
    {
        // Act & Assert
        var act = () => _validator.ValidateDeleteAsync(Guid.NewGuid(), _organizationId);
        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("*AttendanceRecord*not found*");
    }

    [Fact]
    public async Task ValidateDeleteAsync_WhenSessionIsCancelled_ThrowsConflictException()
    {
        // Arrange
        var record = AttendanceRecord.Create(_organizationId, _session.Id, _student.Id);
        _dbContext.AttendanceRecords.Add(record);

        _session.Cancel();
        await _dbContext.SaveChangesAsync();

        // Act & Assert
        var act = () => _validator.ValidateDeleteAsync(record.Id, _organizationId);
        await act.Should().ThrowAsync<ConflictException>()
            .WithMessage("*cancelled session*");
    }

    #endregion

    #region GetById Validation Tests

    [Fact]
    public async Task ValidateGetByIdAsync_WhenStudentCallerAccessesOwnRecord_DoesNotThrow()
    {
        // Arrange
        var record = AttendanceRecord.Create(_organizationId, _session.Id, _student.Id);
        _dbContext.AttendanceRecords.Add(record);
        await _dbContext.SaveChangesAsync();

        // Act & Assert
        var act = () => _validator.ValidateGetByIdAsync(record.Id, _organizationId, _studentUser.Id);
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ValidateGetByIdAsync_WhenStudentCallerAccessesOtherStudent_ThrowsForbiddenAccessException()
    {
        // Arrange
        var otherUser = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            OrganizationId = _organizationId,
            UserName = "other_s@t.com",
            FirstName = "Other",
            LastName = "Student"
        };
        _dbContext.ApplicationUsers.Add(otherUser);
        var otherStudent = Student.Create(otherUser.Id, _organizationId, "STU-OTHER");
        _dbContext.Students.Add(otherStudent);

        var record = AttendanceRecord.Create(_organizationId, _session.Id, _student.Id);
        _dbContext.AttendanceRecords.Add(record);
        await _dbContext.SaveChangesAsync();

        // Act & Assert
        var act = () => _validator.ValidateGetByIdAsync(record.Id, _organizationId, otherUser.Id);
        await act.Should().ThrowAsync<ForbiddenAccessException>()
            .WithMessage("*Students can only view their own attendance records*");
    }

    [Fact]
    public async Task ValidateGetByIdAsync_WhenInstructorAccessesAssignedSession_DoesNotThrow()
    {
        // Arrange
        var record = AttendanceRecord.Create(_organizationId, _session.Id, _student.Id);
        _dbContext.AttendanceRecords.Add(record);
        await _dbContext.SaveChangesAsync();

        // Act & Assert
        var act = () => _validator.ValidateGetByIdAsync(record.Id, _organizationId, _instructorUser.Id);
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ValidateGetByIdAsync_WhenInstructorNotAssigned_ThrowsForbiddenAccessException()
    {
        // Arrange
        var unassignedUser = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            OrganizationId = _organizationId,
            UserName = "unassigned_inst@t.com",
            FirstName = "Unassigned",
            LastName = "Teacher"
        };
        _dbContext.ApplicationUsers.Add(unassignedUser);
        var unassignedInst = Instructor.Create(unassignedUser.Id, _organizationId, "Specialist");
        _dbContext.Instructors.Add(unassignedInst);

        var record = AttendanceRecord.Create(_organizationId, _session.Id, _student.Id);
        _dbContext.AttendanceRecords.Add(record);
        await _dbContext.SaveChangesAsync();

        // Act & Assert
        var act = () => _validator.ValidateGetByIdAsync(record.Id, _organizationId, unassignedUser.Id);
        await act.Should().ThrowAsync<ForbiddenAccessException>()
            .WithMessage("*not authorized to view attendance for this session*");
    }

    #endregion

    #region GetBySessionId Validation Tests

    [Fact]
    public async Task ValidateGetBySessionIdAsync_WhenStudentCaller_ThrowsForbiddenAccessException()
    {
        // Act & Assert
        var act = () => _validator.ValidateGetBySessionIdAsync(_session.Id, _organizationId, _studentUser.Id);
        await act.Should().ThrowAsync<ForbiddenAccessException>()
            .WithMessage("*Students cannot view session attendance rosters*");
    }

    [Fact]
    public async Task ValidateGetBySessionIdAsync_WhenInstructorAssigned_DoesNotThrow()
    {
        // Act & Assert
        var act = () => _validator.ValidateGetBySessionIdAsync(_session.Id, _organizationId, _instructorUser.Id);
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ValidateGetBySessionIdAsync_WhenInstructorNotAssigned_ThrowsForbiddenAccessException()
    {
        // Arrange
        var otherUser = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            OrganizationId = _organizationId,
            UserName = "inst2@t.com",
            FirstName = "Inst",
            LastName = "Two"
        };
        _dbContext.ApplicationUsers.Add(otherUser);
        var otherInst = Instructor.Create(otherUser.Id, _organizationId, "Other");
        _dbContext.Instructors.Add(otherInst);
        await _dbContext.SaveChangesAsync();

        // Act & Assert
        var act = () => _validator.ValidateGetBySessionIdAsync(_session.Id, _organizationId, otherUser.Id);
        await act.Should().ThrowAsync<ForbiddenAccessException>()
            .WithMessage("*not authorized to view attendance for this session*");
    }

    #endregion

    #region GetPercentage Validation Tests

    [Fact]
    public async Task ValidateGetPercentageAsync_WhenStudentNotFound_ThrowsNotFoundException()
    {
        // Act & Assert
        var act = () => _validator.ValidateGetPercentageAsync(Guid.NewGuid(), null, _organizationId);
        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("*Student*not found*");
    }

    [Fact]
    public async Task ValidateGetPercentageAsync_WhenCourseGroupNotFound_ThrowsNotFoundException()
    {
        // Act & Assert
        var act = () => _validator.ValidateGetPercentageAsync(_student.Id, Guid.NewGuid(), _organizationId);
        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("*CourseGroup*not found*");
    }

    [Fact]
    public async Task ValidateGetPercentageAsync_WhenStudentNotEnrolledInGroup_ThrowsConflictException()
    {
        // Arrange
        var otherGroup = CourseGroup.Create(
            _organizationId,
            _course.Id,
            "Cohort 2026-B",
            DateOnly.FromDateTime(DateTime.UtcNow),
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)));
        _dbContext.CourseGroups.Add(otherGroup);
        await _dbContext.SaveChangesAsync();

        // Act & Assert
        var act = () => _validator.ValidateGetPercentageAsync(_student.Id, otherGroup.Id, _organizationId);
        await act.Should().ThrowAsync<ConflictException>()
            .WithMessage("*not enrolled in the specified course group*");
    }

    [Fact]
    public async Task ValidateGetPercentageAsync_WhenStudentCallerAccessesOwnPercentage_DoesNotThrow()
    {
        // Act & Assert
        var act = () => _validator.ValidateGetPercentageAsync(_student.Id, _courseGroup.Id, _organizationId, _studentUser.Id);
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ValidateGetPercentageAsync_WhenStudentCallerAccessesOtherStudent_ThrowsForbiddenAccessException()
    {
        // Arrange
        var otherUser = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            OrganizationId = _organizationId,
            UserName = "stu_other2@t.com",
            FirstName = "Stu",
            LastName = "Other"
        };
        _dbContext.ApplicationUsers.Add(otherUser);
        var otherStudent = Student.Create(otherUser.Id, _organizationId, "STU-OTHER-2");
        _dbContext.Students.Add(otherStudent);
        await _dbContext.SaveChangesAsync();

        // Act & Assert
        var act = () => _validator.ValidateGetPercentageAsync(_student.Id, null, _organizationId, otherUser.Id);
        await act.Should().ThrowAsync<ForbiddenAccessException>()
            .WithMessage("*Students can only view their own attendance data*");
    }

    [Fact]
    public async Task ValidateGetPercentageAsync_WhenInstructorTeachesStudent_DoesNotThrow()
    {
        // Act & Assert
        var act = () => _validator.ValidateGetPercentageAsync(_student.Id, _courseGroup.Id, _organizationId, _instructorUser.Id);
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ValidateGetPercentageAsync_WhenInstructorDoesNotTeachStudent_ThrowsForbiddenAccessException()
    {
        // Arrange
        var otherUser = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            OrganizationId = _organizationId,
            UserName = "inst_other@t.com",
            FirstName = "Inst",
            LastName = "Other"
        };
        _dbContext.ApplicationUsers.Add(otherUser);
        var otherInst = Instructor.Create(otherUser.Id, _organizationId, "Inst");
        _dbContext.Instructors.Add(otherInst);
        await _dbContext.SaveChangesAsync();

        // Act & Assert
        var act = () => _validator.ValidateGetPercentageAsync(_student.Id, null, _organizationId, otherUser.Id);
        await act.Should().ThrowAsync<ForbiddenAccessException>()
            .WithMessage("*not authorized to view attendance data for this student*");
    }

    #endregion
}
