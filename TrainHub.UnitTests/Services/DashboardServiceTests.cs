using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using TrainHub.Application.Common.Exceptions;
using TrainHub.Application.Interfaces;
using TrainHub.Application.Services;
using TrainHub.Domain.Constants;
using TrainHub.Domain.Models;
using TrainHub.Infrastructure.Data;
using TrainHub.UnitTests.Common;

namespace TrainHub.UnitTests.Services;

public class DashboardServiceTests : IDisposable
{
    private readonly TrainHubDbContext _dbContext;
    private readonly Mock<ICurrentOrganizationService> _currentOrgServiceMock;
    private readonly Mock<ILogger<DashboardService>> _loggerMock;
    private readonly DashboardService _service;
    private readonly Guid _organizationId = Guid.NewGuid();
    private readonly Guid _otherOrganizationId = Guid.NewGuid();

    public DashboardServiceTests()
    {
        _dbContext = TestHelpers.CreateInMemoryDbContext();
        _currentOrgServiceMock = new Mock<ICurrentOrganizationService>();
        _currentOrgServiceMock.Setup(s => s.OrganizationId).Returns(_organizationId);
        _loggerMock = new Mock<ILogger<DashboardService>>();

        _service = new DashboardService(
            _dbContext,
            _currentOrgServiceMock.Object,
            _loggerMock.Object);

        // Seed Organizations
        _dbContext.Organizations.AddRange(
            new Organization { Id = _organizationId, Name = "Current Org", Slug = "current-org", IsActive = true },
            new Organization { Id = _otherOrganizationId, Name = "Other Org", Slug = "other-org", IsActive = true }
        );
        _dbContext.SaveChanges();
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }

    #region Context & Tenant Validation

    [Fact]
    public async Task GetDashboardSummaryAsync_WithoutOrganizationContext_ThrowsBadRequestException()
    {
        // Arrange
        _currentOrgServiceMock.Setup(s => s.OrganizationId).Returns((Guid?)null);

        // Act
        var act = () => _service.GetDashboardSummaryAsync();

        // Assert
        await act.Should().ThrowAsync<BadRequestException>()
            .WithMessage("*Organization context is required*");
    }

    [Fact]
    public async Task GetStudentStatisticsAsync_WithoutOrganizationContext_ThrowsBadRequestException()
    {
        // Arrange
        _currentOrgServiceMock.Setup(s => s.OrganizationId).Returns(Guid.Empty);

        // Act
        var act = () => _service.GetStudentStatisticsAsync();

        // Assert
        await act.Should().ThrowAsync<BadRequestException>()
            .WithMessage("*Organization context is required*");
    }

    [Fact]
    public async Task GetCourseStatisticsAsync_WithoutOrganizationContext_ThrowsBadRequestException()
    {
        // Arrange
        _currentOrgServiceMock.Setup(s => s.OrganizationId).Returns((Guid?)null);

        // Act
        var act = () => _service.GetCourseStatisticsAsync();

        // Assert
        await act.Should().ThrowAsync<BadRequestException>()
            .WithMessage("*Organization context is required*");
    }

    [Fact]
    public async Task GetInstructorStatisticsAsync_WithoutOrganizationContext_ThrowsBadRequestException()
    {
        // Arrange
        _currentOrgServiceMock.Setup(s => s.OrganizationId).Returns((Guid?)null);

        // Act
        var act = () => _service.GetInstructorStatisticsAsync();

        // Assert
        await act.Should().ThrowAsync<BadRequestException>()
            .WithMessage("*Organization context is required*");
    }

    [Fact]
    public async Task GetAttendanceStatisticsAsync_WithoutOrganizationContext_ThrowsBadRequestException()
    {
        // Arrange
        _currentOrgServiceMock.Setup(s => s.OrganizationId).Returns((Guid?)null);

        // Act
        var act = () => _service.GetAttendanceStatisticsAsync();

        // Assert
        await act.Should().ThrowAsync<BadRequestException>()
            .WithMessage("*Organization context is required*");
    }

    #endregion

    #region Student Statistics

    [Fact]
    public async Task GetStudentStatisticsAsync_WhenEmpty_ReturnsZeroes()
    {
        // Act
        var result = await _service.GetStudentStatisticsAsync();

        // Assert
        result.Should().NotBeNull();
        result.TotalStudents.Should().Be(0);
        result.ActiveStudents.Should().Be(0);
        result.InactiveStudents.Should().Be(0);
        result.TotalEnrollments.Should().Be(0);
        result.ActiveEnrollments.Should().Be(0);
        result.CompletedEnrollments.Should().Be(0);
        result.CancelledEnrollments.Should().Be(0);
        result.PendingEnrollments.Should().Be(0);
        result.StudentsWithActiveEnrollments.Should().Be(0);
    }

    [Fact]
    public async Task GetStudentStatisticsAsync_CalculatesCorrectStudentAndEnrollmentMetrics()
    {
        // Arrange
        var userActive1 = CreateUser(_organizationId, "active1@test.com", isActive: true);
        var userActive2 = CreateUser(_organizationId, "active2@test.com", isActive: true);
        var userInactive = CreateUser(_organizationId, "inactive@test.com", isActive: false);
        _dbContext.ApplicationUsers.AddRange(userActive1, userActive2, userInactive);

        var student1 = Student.Create(userActive1.Id, _organizationId, "STU-001");
        var student2 = Student.Create(userActive2.Id, _organizationId, "STU-002");
        var student3 = Student.Create(userInactive.Id, _organizationId, "STU-003");
        _dbContext.Students.AddRange(student1, student2, student3);

        var course = Course.Create(_organizationId, "FullStack");
        _dbContext.Courses.Add(course);
        var group1 = CourseGroup.Create(_organizationId, course.Id, "Batch 1", DateOnly.FromDateTime(DateTime.UtcNow), DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(1)));
        var group2 = CourseGroup.Create(_organizationId, course.Id, "Batch 2", DateOnly.FromDateTime(DateTime.UtcNow), DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(1)));
        _dbContext.CourseGroups.AddRange(group1, group2);

        // Enrollments:
        // student1 has 1 Active, 1 Completed
        // student2 has 1 Active, 1 Cancelled, 1 Pending
        // student3 has no enrollments
        var en1 = Enrollment.Create(_organizationId, group1.Id, student1.Id, status: EnrollmentConstants.Statuses.Active);
        var en2 = Enrollment.Create(_organizationId, group2.Id, student1.Id, status: EnrollmentConstants.Statuses.Completed);
        var en3 = Enrollment.Create(_organizationId, group1.Id, student2.Id, status: EnrollmentConstants.Statuses.Active);
        var en4 = Enrollment.Create(_organizationId, group2.Id, student2.Id, status: EnrollmentConstants.Statuses.Cancelled);
        var en5 = Enrollment.Create(_organizationId, group2.Id, student2.Id, status: EnrollmentConstants.Statuses.Pending);
        _dbContext.Enrollments.AddRange(en1, en2, en3, en4, en5);

        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.GetStudentStatisticsAsync();

        // Assert
        result.TotalStudents.Should().Be(3);
        result.ActiveStudents.Should().Be(2);
        result.InactiveStudents.Should().Be(1);
        result.TotalEnrollments.Should().Be(5);
        result.ActiveEnrollments.Should().Be(2);
        result.CompletedEnrollments.Should().Be(1);
        result.CancelledEnrollments.Should().Be(1);
        result.PendingEnrollments.Should().Be(1);
        result.StudentsWithActiveEnrollments.Should().Be(2);
    }

    [Fact]
    public async Task GetStudentStatisticsAsync_EnforcesTenantIsolation_IgnoresOtherTenants()
    {
        // Arrange
        // Current tenant
        var userCurrent = CreateUser(_organizationId, "current@test.com", isActive: true);
        _dbContext.ApplicationUsers.Add(userCurrent);
        var studentCurrent = Student.Create(userCurrent.Id, _organizationId, "STU-CURR");
        _dbContext.Students.Add(studentCurrent);

        // Other tenant
        var userOther = CreateUser(_otherOrganizationId, "other@test.com", isActive: true);
        _dbContext.ApplicationUsers.Add(userOther);
        var studentOther = Student.Create(userOther.Id, _otherOrganizationId, "STU-OTHER");
        _dbContext.Students.Add(studentOther);

        var courseOther = Course.Create(_otherOrganizationId, "Other Course");
        _dbContext.Courses.Add(courseOther);
        var groupOther = CourseGroup.Create(_otherOrganizationId, courseOther.Id, "Other Group", DateOnly.FromDateTime(DateTime.UtcNow), DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(1)));
        _dbContext.CourseGroups.Add(groupOther);
        var enOther = Enrollment.Create(_otherOrganizationId, groupOther.Id, studentOther.Id, status: EnrollmentConstants.Statuses.Active);
        _dbContext.Enrollments.Add(enOther);

        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.GetStudentStatisticsAsync();

        // Assert
        result.TotalStudents.Should().Be(1);
        result.ActiveStudents.Should().Be(1);
        result.TotalEnrollments.Should().Be(0);
        result.ActiveEnrollments.Should().Be(0);
        result.StudentsWithActiveEnrollments.Should().Be(0);
    }

    #endregion

    #region Course Statistics

    [Fact]
    public async Task GetCourseStatisticsAsync_WhenEmpty_ReturnsZeroes()
    {
        // Act
        var result = await _service.GetCourseStatisticsAsync();

        // Assert
        result.Should().NotBeNull();
        result.TotalCourses.Should().Be(0);
        result.ActiveCourses.Should().Be(0);
        result.InactiveCourses.Should().Be(0);
        result.DraftCourses.Should().Be(0);
        result.ArchivedCourses.Should().Be(0);
        result.TotalCourseGroups.Should().Be(0);
        result.ActiveCourseGroups.Should().Be(0);
        result.CompletedCourseGroups.Should().Be(0);
        result.CancelledCourseGroups.Should().Be(0);
        result.DraftCourseGroups.Should().Be(0);
        result.TotalEnrollments.Should().Be(0);
        result.ActiveEnrollments.Should().Be(0);
    }

    [Fact]
    public async Task GetCourseStatisticsAsync_CalculatesCorrectCourseAndGroupBreakdown()
    {
        // Arrange
        var courseDraft = Course.Create(_organizationId, "C# Draft", status: CourseConstants.Statuses.Draft);
        var courseActive1 = Course.Create(_organizationId, "C# Active 1", status: CourseConstants.Statuses.Active);
        var courseActive2 = Course.Create(_organizationId, "C# Active 2", status: CourseConstants.Statuses.Active);
        var courseInactive = Course.Create(_organizationId, "C# Inactive", status: CourseConstants.Statuses.Inactive);
        var courseArchived = Course.Create(_organizationId, "C# Archived", status: CourseConstants.Statuses.Archived);
        _dbContext.Courses.AddRange(courseDraft, courseActive1, courseActive2, courseInactive, courseArchived);

        var groupActive = CourseGroup.Create(_organizationId, courseActive1.Id, "G1", DateOnly.FromDateTime(DateTime.UtcNow), DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(1)), status: CourseGroupConstants.Statuses.Active);
        var groupDraft = CourseGroup.Create(_organizationId, courseActive1.Id, "G2", DateOnly.FromDateTime(DateTime.UtcNow), DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(1)), status: CourseGroupConstants.Statuses.Draft);
        var groupCompleted = CourseGroup.Create(_organizationId, courseActive1.Id, "G3", DateOnly.FromDateTime(DateTime.UtcNow), DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(1)), status: CourseGroupConstants.Statuses.Completed);
        var groupCancelled = CourseGroup.Create(_organizationId, courseActive1.Id, "G4", DateOnly.FromDateTime(DateTime.UtcNow), DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(1)), status: CourseGroupConstants.Statuses.Cancelled);
        _dbContext.CourseGroups.AddRange(groupActive, groupDraft, groupCompleted, groupCancelled);

        var user = CreateUser(_organizationId, "student@test.com", isActive: true);
        _dbContext.ApplicationUsers.Add(user);
        var student = Student.Create(user.Id, _organizationId, "STU-001");
        _dbContext.Students.Add(student);

        var en1 = Enrollment.Create(_organizationId, groupActive.Id, student.Id, status: EnrollmentConstants.Statuses.Active);
        var en2 = Enrollment.Create(_organizationId, groupActive.Id, student.Id, status: EnrollmentConstants.Statuses.Completed);
        _dbContext.Enrollments.AddRange(en1, en2);

        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.GetCourseStatisticsAsync();

        // Assert
        result.TotalCourses.Should().Be(5);
        result.DraftCourses.Should().Be(1);
        result.ActiveCourses.Should().Be(2);
        result.InactiveCourses.Should().Be(1);
        result.ArchivedCourses.Should().Be(1);

        result.TotalCourseGroups.Should().Be(4);
        result.ActiveCourseGroups.Should().Be(1);
        result.DraftCourseGroups.Should().Be(1);
        result.CompletedCourseGroups.Should().Be(1);
        result.CancelledCourseGroups.Should().Be(1);

        result.TotalEnrollments.Should().Be(2);
        result.ActiveEnrollments.Should().Be(1);
    }

    [Fact]
    public async Task GetCourseStatisticsAsync_EnforcesTenantIsolation_IgnoresOtherTenants()
    {
        // Arrange
        var courseCurrent = Course.Create(_organizationId, "Current Course", status: CourseConstants.Statuses.Active);
        _dbContext.Courses.Add(courseCurrent);

        var courseOther = Course.Create(_otherOrganizationId, "Other Course", status: CourseConstants.Statuses.Active);
        _dbContext.Courses.Add(courseOther);
        var groupOther = CourseGroup.Create(_otherOrganizationId, courseOther.Id, "Other Group", DateOnly.FromDateTime(DateTime.UtcNow), DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(1)), status: CourseGroupConstants.Statuses.Active);
        _dbContext.CourseGroups.Add(groupOther);

        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.GetCourseStatisticsAsync();

        // Assert
        result.TotalCourses.Should().Be(1);
        result.ActiveCourses.Should().Be(1);
        result.TotalCourseGroups.Should().Be(0);
    }

    #endregion

    #region Instructor Statistics

    [Fact]
    public async Task GetInstructorStatisticsAsync_WhenEmpty_ReturnsZeroes()
    {
        // Act
        var result = await _service.GetInstructorStatisticsAsync();

        // Assert
        result.Should().NotBeNull();
        result.TotalInstructors.Should().Be(0);
        result.ActiveInstructors.Should().Be(0);
        result.InactiveInstructors.Should().Be(0);
        result.AssignedInstructors.Should().Be(0);
        result.ActiveAssignedInstructors.Should().Be(0);
    }

    [Fact]
    public async Task GetInstructorStatisticsAsync_CalculatesCorrectInstructorAndAssignmentMetrics()
    {
        // Arrange
        var userActive1 = CreateUser(_organizationId, "inst1@test.com", isActive: true);
        var userActive2 = CreateUser(_organizationId, "inst2@test.com", isActive: true);
        var userInactive = CreateUser(_organizationId, "inst3@test.com", isActive: false);
        _dbContext.ApplicationUsers.AddRange(userActive1, userActive2, userInactive);

        var inst1 = Instructor.Create(userActive1.Id, _organizationId, "Backend Dev");
        var inst2 = Instructor.Create(userActive2.Id, _organizationId, "Frontend Dev");
        var inst3 = Instructor.Create(userInactive.Id, _organizationId, "DevOps");
        _dbContext.Instructors.AddRange(inst1, inst2, inst3);

        var course = Course.Create(_organizationId, "Course");
        _dbContext.Courses.Add(course);

        // inst1 is assigned to an Active group
        var group1 = CourseGroup.Create(_organizationId, course.Id, "G1", DateOnly.FromDateTime(DateTime.UtcNow), DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(1)),
            instructorId: inst1.Id, status: CourseGroupConstants.Statuses.Active);
        // inst2 is assigned to a Completed group
        var group2 = CourseGroup.Create(_organizationId, course.Id, "G2", DateOnly.FromDateTime(DateTime.UtcNow), DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(1)),
            instructorId: inst2.Id, status: CourseGroupConstants.Statuses.Completed);
        // inst3 has no groups assigned
        _dbContext.CourseGroups.AddRange(group1, group2);

        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.GetInstructorStatisticsAsync();

        // Assert
        result.TotalInstructors.Should().Be(3);
        result.ActiveInstructors.Should().Be(2);
        result.InactiveInstructors.Should().Be(1);
        result.AssignedInstructors.Should().Be(2); // inst1 and inst2
        result.ActiveAssignedInstructors.Should().Be(1); // inst1
    }

    [Fact]
    public async Task GetInstructorStatisticsAsync_EnforcesTenantIsolation_IgnoresOtherTenants()
    {
        // Arrange
        var userCurrent = CreateUser(_organizationId, "currentInst@test.com", isActive: true);
        _dbContext.ApplicationUsers.Add(userCurrent);
        var instCurrent = Instructor.Create(userCurrent.Id, _organizationId, "Security");
        _dbContext.Instructors.Add(instCurrent);

        var userOther = CreateUser(_otherOrganizationId, "otherInst@test.com", isActive: true);
        _dbContext.ApplicationUsers.Add(userOther);
        var instOther = Instructor.Create(userOther.Id, _otherOrganizationId, "Cloud");
        _dbContext.Instructors.Add(instOther);

        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.GetInstructorStatisticsAsync();

        // Assert
        result.TotalInstructors.Should().Be(1);
        result.ActiveInstructors.Should().Be(1);
        result.AssignedInstructors.Should().Be(0);
    }

    #endregion

    #region Attendance Statistics

    [Fact]
    public async Task GetAttendanceStatisticsAsync_WhenEmpty_ReturnsZeroesWithZeroPercentage()
    {
        // Act
        var result = await _service.GetAttendanceStatisticsAsync();

        // Assert
        result.Should().NotBeNull();
        result.TotalRecords.Should().Be(0);
        result.AttendedCount.Should().Be(0);
        result.PresentCount.Should().Be(0);
        result.LateCount.Should().Be(0);
        result.AbsentCount.Should().Be(0);
        result.ExcusedCount.Should().Be(0);
        result.AttendancePercentage.Should().Be(0.00m);
        result.TotalSessions.Should().Be(0);
        result.ScheduledSessions.Should().Be(0);
        result.CompletedSessions.Should().Be(0);
        result.CancelledSessions.Should().Be(0);
        result.DraftSessions.Should().Be(0);
    }

    [Fact]
    public async Task GetAttendanceStatisticsAsync_CalculatesCorrectAttendanceAndSessionMetrics()
    {
        // Arrange
        var course = Course.Create(_organizationId, "FullStack");
        _dbContext.Courses.Add(course);
        var group = CourseGroup.Create(_organizationId, course.Id, "Batch A", DateOnly.FromDateTime(DateTime.UtcNow), DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(1)));
        _dbContext.CourseGroups.Add(group);

        var session1 = Session.Create(_organizationId, group.Id, DateTime.UtcNow.AddDays(1), DateTime.UtcNow.AddDays(1).AddHours(2), status: SessionConstants.Statuses.Scheduled);
        var session2 = Session.Create(_organizationId, group.Id, DateTime.UtcNow.AddDays(2), DateTime.UtcNow.AddDays(2).AddHours(2), status: SessionConstants.Statuses.Completed);
        var session3 = Session.Create(_organizationId, group.Id, DateTime.UtcNow.AddDays(3), DateTime.UtcNow.AddDays(3).AddHours(2), status: SessionConstants.Statuses.Cancelled);
        var session4 = Session.Create(_organizationId, group.Id, DateTime.UtcNow.AddDays(4), DateTime.UtcNow.AddDays(4).AddHours(2), status: SessionConstants.Statuses.Draft);
        _dbContext.Sessions.AddRange(session1, session2, session3, session4);

        var user = CreateUser(_organizationId, "stu@test.com", isActive: true);
        _dbContext.ApplicationUsers.Add(user);
        var student = Student.Create(user.Id, _organizationId, "STU-001");
        _dbContext.Students.Add(student);

        // 4 attendance records: 2 Present, 1 Late, 1 Absent (Total: 4, Attended: 3 => 3/4 = 75.00%)
        var rec1 = AttendanceRecord.Create(_organizationId, session1.Id, student.Id, AttendanceConstants.Statuses.Present);
        var rec2 = AttendanceRecord.Create(_organizationId, session1.Id, student.Id, AttendanceConstants.Statuses.Present);
        var rec3 = AttendanceRecord.Create(_organizationId, session2.Id, student.Id, AttendanceConstants.Statuses.Late);
        var rec4 = AttendanceRecord.Create(_organizationId, session2.Id, student.Id, AttendanceConstants.Statuses.Absent);
        _dbContext.AttendanceRecords.AddRange(rec1, rec2, rec3, rec4);

        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.GetAttendanceStatisticsAsync();

        // Assert
        result.TotalRecords.Should().Be(4);
        result.PresentCount.Should().Be(2);
        result.LateCount.Should().Be(1);
        result.AbsentCount.Should().Be(1);
        result.ExcusedCount.Should().Be(0);
        result.AttendedCount.Should().Be(3);
        result.AttendancePercentage.Should().Be(75.00m);

        result.TotalSessions.Should().Be(4);
        result.ScheduledSessions.Should().Be(1);
        result.CompletedSessions.Should().Be(1);
        result.CancelledSessions.Should().Be(1);
        result.DraftSessions.Should().Be(1);
    }

    [Fact]
    public async Task GetAttendanceStatisticsAsync_FilteredByCourseGroup_ReturnsGroupSpecificMetrics()
    {
        // Arrange
        var course = Course.Create(_organizationId, "Course");
        _dbContext.Courses.Add(course);
        var group1 = CourseGroup.Create(_organizationId, course.Id, "Group 1", DateOnly.FromDateTime(DateTime.UtcNow), DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(1)));
        var group2 = CourseGroup.Create(_organizationId, course.Id, "Group 2", DateOnly.FromDateTime(DateTime.UtcNow), DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(1)));
        _dbContext.CourseGroups.AddRange(group1, group2);

        var sGroup1 = Session.Create(_organizationId, group1.Id, DateTime.UtcNow.AddDays(1), DateTime.UtcNow.AddDays(1).AddHours(2), status: SessionConstants.Statuses.Completed);
        var sGroup2 = Session.Create(_organizationId, group2.Id, DateTime.UtcNow.AddDays(2), DateTime.UtcNow.AddDays(2).AddHours(2), status: SessionConstants.Statuses.Scheduled);
        _dbContext.Sessions.AddRange(sGroup1, sGroup2);

        var user = CreateUser(_organizationId, "stu@test.com", isActive: true);
        _dbContext.ApplicationUsers.Add(user);
        var student = Student.Create(user.Id, _organizationId, "STU-001");
        _dbContext.Students.Add(student);

        var recGroup1 = AttendanceRecord.Create(_organizationId, sGroup1.Id, student.Id, AttendanceConstants.Statuses.Present);
        var recGroup2 = AttendanceRecord.Create(_organizationId, sGroup2.Id, student.Id, AttendanceConstants.Statuses.Absent);
        _dbContext.AttendanceRecords.AddRange(recGroup1, recGroup2);

        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.GetAttendanceStatisticsAsync(group1.Id);

        // Assert
        result.CourseGroupId.Should().Be(group1.Id);
        result.TotalRecords.Should().Be(1);
        result.PresentCount.Should().Be(1);
        result.AbsentCount.Should().Be(0);
        result.AttendedCount.Should().Be(1);
        result.AttendancePercentage.Should().Be(100.00m);
        result.TotalSessions.Should().Be(1);
        result.CompletedSessions.Should().Be(1);
        result.ScheduledSessions.Should().Be(0);
    }

    [Fact]
    public async Task GetAttendanceStatisticsAsync_WithNonExistentCourseGroup_ThrowsNotFoundException()
    {
        // Act
        var act = () => _service.GetAttendanceStatisticsAsync(Guid.NewGuid());

        // Assert
        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("*CourseGroup*");
    }

    [Fact]
    public async Task GetAttendanceStatisticsAsync_WithEmptyCourseGroupId_ThrowsBadRequestException()
    {
        // Act
        var act = () => _service.GetAttendanceStatisticsAsync(Guid.Empty);

        // Assert
        await act.Should().ThrowAsync<BadRequestException>()
            .WithMessage("*CourseGroup ID cannot be empty*");
    }

    [Fact]
    public async Task GetAttendanceStatisticsAsync_WithOtherTenantCourseGroup_ThrowsNotFoundException()
    {
        // Arrange
        var courseOther = Course.Create(_otherOrganizationId, "Other Course");
        _dbContext.Courses.Add(courseOther);
        var groupOther = CourseGroup.Create(_otherOrganizationId, courseOther.Id, "Other Group", DateOnly.FromDateTime(DateTime.UtcNow), DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(1)));
        _dbContext.CourseGroups.Add(groupOther);
        await _dbContext.SaveChangesAsync();

        // Act
        var act = () => _service.GetAttendanceStatisticsAsync(groupOther.Id);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("*CourseGroup*");
    }

    [Fact]
    public async Task GetAttendanceStatisticsAsync_EnforcesTenantIsolation_IgnoresOtherTenants()
    {
        // Arrange
        var courseCurrent = Course.Create(_organizationId, "Curr Course");
        _dbContext.Courses.Add(courseCurrent);
        var groupCurrent = CourseGroup.Create(_organizationId, courseCurrent.Id, "Curr Group", DateOnly.FromDateTime(DateTime.UtcNow), DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(1)));
        _dbContext.CourseGroups.Add(groupCurrent);
        var sCurrent = Session.Create(_organizationId, groupCurrent.Id, DateTime.UtcNow.AddDays(1), DateTime.UtcNow.AddDays(1).AddHours(2), status: SessionConstants.Statuses.Scheduled);
        _dbContext.Sessions.Add(sCurrent);

        var courseOther = Course.Create(_otherOrganizationId, "Other Course");
        _dbContext.Courses.Add(courseOther);
        var groupOther = CourseGroup.Create(_otherOrganizationId, courseOther.Id, "Other Group", DateOnly.FromDateTime(DateTime.UtcNow), DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(1)));
        _dbContext.CourseGroups.Add(groupOther);
        var sOther = Session.Create(_otherOrganizationId, groupOther.Id, DateTime.UtcNow.AddDays(1), DateTime.UtcNow.AddDays(1).AddHours(2), status: SessionConstants.Statuses.Completed);
        _dbContext.Sessions.Add(sOther);

        var userOther = CreateUser(_otherOrganizationId, "otherStu@test.com", isActive: true);
        _dbContext.ApplicationUsers.Add(userOther);
        var studentOther = Student.Create(userOther.Id, _otherOrganizationId, "STU-OTHER");
        _dbContext.Students.Add(studentOther);
        var recOther = AttendanceRecord.Create(_otherOrganizationId, sOther.Id, studentOther.Id, AttendanceConstants.Statuses.Present);
        _dbContext.AttendanceRecords.Add(recOther);

        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.GetAttendanceStatisticsAsync();

        // Assert
        result.TotalSessions.Should().Be(1);
        result.ScheduledSessions.Should().Be(1);
        result.CompletedSessions.Should().Be(0);
        result.TotalRecords.Should().Be(0);
    }

    #endregion

    #region Dashboard Summary Query

    [Fact]
    public async Task GetDashboardSummaryAsync_ReturnsAggregatedSummaryForCurrentTenant()
    {
        // Arrange
        // Current tenant data:
        var user = CreateUser(_organizationId, "john@test.com", isActive: true);
        _dbContext.ApplicationUsers.Add(user);
        var student = Student.Create(user.Id, _organizationId, "STU-JOHN");
        _dbContext.Students.Add(student);

        var instructor = Instructor.Create(user.Id, _organizationId, "Architecture");
        _dbContext.Instructors.Add(instructor);

        var course = Course.Create(_organizationId, "Design Patterns", status: CourseConstants.Statuses.Active);
        _dbContext.Courses.Add(course);

        var group = CourseGroup.Create(_organizationId, course.Id, "DP Batch 1", DateOnly.FromDateTime(DateTime.UtcNow), DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(1)),
            instructorId: instructor.Id, status: CourseGroupConstants.Statuses.Active);
        _dbContext.CourseGroups.Add(group);

        var enrollment = Enrollment.Create(_organizationId, group.Id, student.Id, status: EnrollmentConstants.Statuses.Active);
        _dbContext.Enrollments.Add(enrollment);

        var session = Session.Create(_organizationId, group.Id, DateTime.UtcNow.AddDays(1), DateTime.UtcNow.AddDays(1).AddHours(2), status: SessionConstants.Statuses.Scheduled);
        _dbContext.Sessions.Add(session);

        var attendance = AttendanceRecord.Create(_organizationId, session.Id, student.Id, AttendanceConstants.Statuses.Present);
        _dbContext.AttendanceRecords.Add(attendance);

        await _dbContext.SaveChangesAsync();

        // Act
        var summary = await _service.GetDashboardSummaryAsync();

        // Assert
        summary.Should().NotBeNull();
        summary.OrganizationId.Should().Be(_organizationId);

        summary.Students.TotalStudents.Should().Be(1);
        summary.Students.ActiveStudents.Should().Be(1);
        summary.Students.TotalEnrollments.Should().Be(1);

        summary.Courses.TotalCourses.Should().Be(1);
        summary.Courses.ActiveCourses.Should().Be(1);
        summary.Courses.TotalCourseGroups.Should().Be(1);
        summary.Courses.ActiveCourseGroups.Should().Be(1);

        summary.Instructors.TotalInstructors.Should().Be(1);
        summary.Instructors.ActiveInstructors.Should().Be(1);
        summary.Instructors.AssignedInstructors.Should().Be(1);
        summary.Instructors.ActiveAssignedInstructors.Should().Be(1);

        summary.Attendance.TotalRecords.Should().Be(1);
        summary.Attendance.PresentCount.Should().Be(1);
        summary.Attendance.AttendancePercentage.Should().Be(100.00m);
        summary.Attendance.TotalSessions.Should().Be(1);
        summary.Attendance.ScheduledSessions.Should().Be(1);
    }

    #endregion

    #region Helpers

    private static ApplicationUser CreateUser(Guid organizationId, string email, bool isActive)
    {
        return new ApplicationUser
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            UserName = email,
            NormalizedUserName = email.ToUpperInvariant(),
            Email = email,
            NormalizedEmail = email.ToUpperInvariant(),
            FirstName = "Test",
            LastName = "User",
            IsActive = isActive
        };
    }

    #endregion
}
