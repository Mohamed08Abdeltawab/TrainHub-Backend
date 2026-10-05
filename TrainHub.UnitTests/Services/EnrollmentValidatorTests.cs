using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using TrainHub.Application.Common.Exceptions;
using TrainHub.Application.Features.Enrollments.DTOs;
using TrainHub.Application.Services;
using TrainHub.Domain.Constants;
using TrainHub.Domain.Models;
using TrainHub.Infrastructure.Data;
using TrainHub.UnitTests.Common;

namespace TrainHub.UnitTests.Services;

public class EnrollmentValidatorTests : IDisposable
{
    private readonly TrainHubDbContext _dbContext;
    private readonly Mock<ILogger<EnrollmentValidator>> _loggerMock;
    private readonly EnrollmentValidator _validator;
    private readonly Guid _organizationId = Guid.NewGuid();
    private readonly CourseGroup _courseGroup;
    private readonly Student _student;
    private readonly ApplicationUser _studentUser;

    public EnrollmentValidatorTests()
    {
        _dbContext = TestHelpers.CreateInMemoryDbContext();
        _loggerMock = new Mock<ILogger<EnrollmentValidator>>();
        _validator = new EnrollmentValidator(_dbContext, _loggerMock.Object);

        // Seed org
        var org = new Organization
        {
            Id = _organizationId,
            Name = "Main Org",
            Slug = "main-org",
            IsActive = true
        };
        _dbContext.Organizations.Add(org);

        // Seed course & group
        var course = Course.Create(_organizationId, "C# Web API");
        _dbContext.Courses.Add(course);

        _courseGroup = CourseGroup.Create(
            _organizationId,
            course.Id,
            "Group A",
            new DateOnly(2026, 10, 1),
            new DateOnly(2026, 12, 1),
            capacity: 2);
        _dbContext.CourseGroups.Add(_courseGroup);

        // Seed user and student
        _studentUser = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            OrganizationId = _organizationId,
            FirstName = "Alice",
            LastName = "Smith",
            IsActive = true
        };
        _dbContext.ApplicationUsers.Add(_studentUser);

        _student = Student.Create(_studentUser.Id, _organizationId, "STU-001");
        _dbContext.Students.Add(_student);

        _dbContext.SaveChanges();
    }

    public void Dispose()
    {
        _dbContext.Database.EnsureDeleted();
        _dbContext.Dispose();
    }

    #region ValidateQuery

    [Fact]
    public void ValidateQuery_ValidParameters_DoesNotThrow()
    {
        var query = new EnrollmentQueryParameters
        {
            PageNumber = 1,
            PageSize = 10,
            SortBy = "CreatedAt",
            Status = EnrollmentConstants.Statuses.Active
        };

        var act = () => _validator.ValidateQuery(query);

        act.Should().NotThrow();
    }

    [Fact]
    public void ValidateQuery_InvalidStatus_ThrowsValidationException()
    {
        var query = new EnrollmentQueryParameters
        {
            Status = 99
        };

        var act = () => _validator.ValidateQuery(query);

        act.Should().Throw<ValidationException>()
            .Which.Errors.Should().ContainKey(nameof(query.Status));
    }

    [Fact]
    public void ValidateQuery_InvalidSortField_ThrowsValidationException()
    {
        var query = new EnrollmentQueryParameters
        {
            SortBy = "InvalidField"
        };

        var act = () => _validator.ValidateQuery(query);

        act.Should().Throw<ValidationException>()
            .Which.Errors.Should().ContainKey(nameof(query.SortBy));
    }

    #endregion

    #region ValidateCreateAsync

    [Fact]
    public async Task ValidateCreateAsync_ValidDto_DoesNotThrow()
    {
        var dto = new CreateEnrollmentDto
        {
            CourseGroupId = _courseGroup.Id,
            StudentId = _student.Id,
            PaidAmount = 100m,
            Status = EnrollmentConstants.Statuses.Active
        };

        var act = () => _validator.ValidateCreateAsync(dto, _organizationId, CancellationToken.None);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ValidateCreateAsync_NonExistentOrganization_ThrowsNotFoundException()
    {
        var dto = new CreateEnrollmentDto
        {
            CourseGroupId = _courseGroup.Id,
            StudentId = _student.Id
        };

        var act = () => _validator.ValidateCreateAsync(dto, Guid.NewGuid(), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("*Organization*not found*");
    }

    [Fact]
    public async Task ValidateCreateAsync_NonExistentCourseGroup_ThrowsNotFoundException()
    {
        var dto = new CreateEnrollmentDto
        {
            CourseGroupId = Guid.NewGuid(),
            StudentId = _student.Id
        };

        var act = () => _validator.ValidateCreateAsync(dto, _organizationId, CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("*CourseGroup*not found*");
    }

    [Fact]
    public async Task ValidateCreateAsync_CourseGroupInDifferentOrg_ThrowsNotFoundException()
    {
        var otherOrgId = Guid.NewGuid();
        var dto = new CreateEnrollmentDto
        {
            CourseGroupId = _courseGroup.Id,
            StudentId = _student.Id
        };

        var act = () => _validator.ValidateCreateAsync(dto, otherOrgId, CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task ValidateCreateAsync_NonActiveCourseGroup_ThrowsConflictException()
    {
        // Cancel the course group
        _courseGroup.Cancel();
        await _dbContext.SaveChangesAsync();

        var dto = new CreateEnrollmentDto
        {
            CourseGroupId = _courseGroup.Id,
            StudentId = _student.Id
        };

        var act = () => _validator.ValidateCreateAsync(dto, _organizationId, CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>()
            .WithMessage("*not active*");
    }

    [Fact]
    public async Task ValidateCreateAsync_CourseGroupAtCapacity_ThrowsConflictException()
    {
        // Group capacity is 2
        var student2 = Student.Create(Guid.NewGuid(), _organizationId, "STU-002");
        var student3 = Student.Create(Guid.NewGuid(), _organizationId, "STU-003");
        _dbContext.Students.AddRange(student2, student3);

        _dbContext.Enrollments.Add(Enrollment.Create(_organizationId, _courseGroup.Id, student2.Id));
        _dbContext.Enrollments.Add(Enrollment.Create(_organizationId, _courseGroup.Id, student3.Id));
        await _dbContext.SaveChangesAsync();

        var dto = new CreateEnrollmentDto
        {
            CourseGroupId = _courseGroup.Id,
            StudentId = _student.Id
        };

        var act = () => _validator.ValidateCreateAsync(dto, _organizationId, CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>()
            .WithMessage("*maximum capacity*");
    }

    [Fact]
    public async Task ValidateCreateAsync_NonExistentStudent_ThrowsNotFoundException()
    {
        var dto = new CreateEnrollmentDto
        {
            CourseGroupId = _courseGroup.Id,
            StudentId = Guid.NewGuid()
        };

        var act = () => _validator.ValidateCreateAsync(dto, _organizationId, CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("*Student*not found*");
    }

    [Fact]
    public async Task ValidateCreateAsync_InactiveStudentUser_ThrowsConflictException()
    {
        _studentUser.IsActive = false;
        await _dbContext.SaveChangesAsync();

        var dto = new CreateEnrollmentDto
        {
            CourseGroupId = _courseGroup.Id,
            StudentId = _student.Id
        };

        var act = () => _validator.ValidateCreateAsync(dto, _organizationId, CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>()
            .WithMessage("*inactive*");
    }

    [Fact]
    public async Task ValidateCreateAsync_StudentAlreadyEnrolled_ThrowsConflictException()
    {
        _dbContext.Enrollments.Add(Enrollment.Create(_organizationId, _courseGroup.Id, _student.Id));
        await _dbContext.SaveChangesAsync();

        var dto = new CreateEnrollmentDto
        {
            CourseGroupId = _courseGroup.Id,
            StudentId = _student.Id
        };

        var act = () => _validator.ValidateCreateAsync(dto, _organizationId, CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>()
            .WithMessage("*already enrolled*");
    }

    #endregion

    #region ValidateUpdateAsync

    [Fact]
    public async Task ValidateUpdateAsync_NonExistentEnrollment_ThrowsNotFoundException()
    {
        var dto = new UpdateEnrollmentDto { PaidAmount = 50m };

        var act = () => _validator.ValidateUpdateAsync(Guid.NewGuid(), dto, _organizationId, CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task ValidateUpdateAsync_CancelledEnrollment_ThrowsConflictException()
    {
        var enrollment = Enrollment.Create(_organizationId, _courseGroup.Id, _student.Id, status: EnrollmentConstants.Statuses.Cancelled);
        _dbContext.Enrollments.Add(enrollment);
        await _dbContext.SaveChangesAsync();

        var dto = new UpdateEnrollmentDto { PaidAmount = 100m };

        var act = () => _validator.ValidateUpdateAsync(enrollment.Id, dto, _organizationId, CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>()
            .WithMessage("*cancelled*");
    }

    [Fact]
    public async Task ValidateUpdateAsync_CompletedToPending_ThrowsConflictException()
    {
        var enrollment = Enrollment.Create(_organizationId, _courseGroup.Id, _student.Id, status: EnrollmentConstants.Statuses.Completed);
        _dbContext.Enrollments.Add(enrollment);
        await _dbContext.SaveChangesAsync();

        var dto = new UpdateEnrollmentDto { Status = EnrollmentConstants.Statuses.Pending };

        var act = () => _validator.ValidateUpdateAsync(enrollment.Id, dto, _organizationId, CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>()
            .WithMessage("*completed enrollment back to pending*");
    }

    #endregion

    #region ValidateUpdateStatusAsync

    [Fact]
    public async Task ValidateUpdateStatusAsync_InvalidStatus_ThrowsValidationException()
    {
        var act = () => _validator.ValidateUpdateStatusAsync(Guid.NewGuid(), 99, CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task ValidateUpdateStatusAsync_NonExistentEnrollment_ThrowsNotFoundException()
    {
        var act = () => _validator.ValidateUpdateStatusAsync(Guid.NewGuid(), EnrollmentConstants.Statuses.Active, CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task ValidateUpdateStatusAsync_CancelledEnrollmentToCompleted_ThrowsConflictException()
    {
        var enrollment = Enrollment.Create(_organizationId, _courseGroup.Id, _student.Id, status: EnrollmentConstants.Statuses.Cancelled);
        _dbContext.Enrollments.Add(enrollment);
        await _dbContext.SaveChangesAsync();

        var act = () => _validator.ValidateUpdateStatusAsync(enrollment.Id, EnrollmentConstants.Statuses.Completed, CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>()
            .WithMessage("*cancelled*");
    }

    #endregion

    #region ValidateDeleteAsync

    [Fact]
    public async Task ValidateDeleteAsync_NonExistentEnrollment_ThrowsNotFoundException()
    {
        var act = () => _validator.ValidateDeleteAsync(Guid.NewGuid(), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task ValidateDeleteAsync_EnrollmentWithAttendance_ThrowsConflictException()
    {
        var enrollment = Enrollment.Create(_organizationId, _courseGroup.Id, _student.Id);
        _dbContext.Enrollments.Add(enrollment);

        var session = Session.Create(
            _organizationId,
            _courseGroup.Id,
            DateTime.UtcNow,
            DateTime.UtcNow.AddHours(2),
            status: 1);
        _dbContext.Sessions.Add(session);

        var attendance = AttendanceRecord.Create(
            _organizationId,
            session.Id,
            _student.Id,
            status: 1);
        _dbContext.AttendanceRecords.Add(attendance);
        await _dbContext.SaveChangesAsync();

        var act = () => _validator.ValidateDeleteAsync(enrollment.Id, CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>()
            .WithMessage("*attendance records*");
    }

    [Fact]
    public async Task ValidateDeleteAsync_ValidEnrollmentWithoutAttendance_DoesNotThrow()
    {
        var enrollment = Enrollment.Create(_organizationId, _courseGroup.Id, _student.Id);
        _dbContext.Enrollments.Add(enrollment);
        await _dbContext.SaveChangesAsync();

        var act = () => _validator.ValidateDeleteAsync(enrollment.Id, CancellationToken.None);

        await act.Should().NotThrowAsync();
    }

    #endregion

    #region Duplicate & Capacity

    [Fact]
    public async Task ValidateCreateAsync_StudentEnrolledInDifferentGroup_DoesNotThrow()
    {
        // Arrange - Enroll in group 1
        _dbContext.Enrollments.Add(Enrollment.Create(_organizationId, _courseGroup.Id, _student.Id));

        // Create a different group
        var otherGroup = CourseGroup.Create(
            _organizationId,
            _courseGroup.CourseId,
            "Group B",
            new DateOnly(2026, 10, 1),
            new DateOnly(2026, 12, 1));
        _dbContext.CourseGroups.Add(otherGroup);
        await _dbContext.SaveChangesAsync();

        var dto = new CreateEnrollmentDto
        {
            CourseGroupId = otherGroup.Id,
            StudentId = _student.Id
        };

        // Act - Enroll same student in group B
        var act = () => _validator.ValidateCreateAsync(dto, _organizationId, CancellationToken.None);

        // Assert - Should succeed
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ValidateCreateAsync_CancelledEnrollmentDoesNotConsumeCapacity_AllowsNewEnrollment()
    {
        // Arrange - Capacity is 2
        var s2 = Student.Create(Guid.NewGuid(), _organizationId, "STU-002");
        var s3 = Student.Create(Guid.NewGuid(), _organizationId, "STU-003");
        _dbContext.Students.AddRange(s2, s3);

        // One active, one cancelled enrollment
        _dbContext.Enrollments.Add(Enrollment.Create(_organizationId, _courseGroup.Id, s2.Id, status: EnrollmentConstants.Statuses.Active));
        _dbContext.Enrollments.Add(Enrollment.Create(_organizationId, _courseGroup.Id, s3.Id, status: EnrollmentConstants.Statuses.Cancelled));
        await _dbContext.SaveChangesAsync();

        var dto = new CreateEnrollmentDto
        {
            CourseGroupId = _courseGroup.Id,
            StudentId = _student.Id
        };

        // Act - Active count is 1 < 2, so should pass
        var act = () => _validator.ValidateCreateAsync(dto, _organizationId, CancellationToken.None);

        // Assert
        await act.Should().NotThrowAsync();
    }

    #endregion
}
