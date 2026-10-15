using FluentAssertions;
using Microsoft.EntityFrameworkCore.Storage;
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

    #region Percentage Tests

    [Fact]
    public async Task GetPercentageAsync_WithRecords_CalculatesCorrectPercentageAndBreakdown()
    {
        // Arrange
        // Session 1: Present
        var r1 = AttendanceRecord.Create(_organizationId, _session.Id, _student.Id, AttendanceConstants.Statuses.Present);
        _dbContext.AttendanceRecords.Add(r1);

        // Session 2: Late
        var s2 = Session.Create(_organizationId, _courseGroup.Id, DateTime.UtcNow.AddDays(2), DateTime.UtcNow.AddDays(2).AddHours(1));
        _dbContext.Sessions.Add(s2);
        var r2 = AttendanceRecord.Create(_organizationId, s2.Id, _student.Id, AttendanceConstants.Statuses.Late);
        _dbContext.AttendanceRecords.Add(r2);

        // Session 3: Absent
        var s3 = Session.Create(_organizationId, _courseGroup.Id, DateTime.UtcNow.AddDays(3), DateTime.UtcNow.AddDays(3).AddHours(1));
        _dbContext.Sessions.Add(s3);
        var r3 = AttendanceRecord.Create(_organizationId, s3.Id, _student.Id, AttendanceConstants.Statuses.Absent);
        _dbContext.AttendanceRecords.Add(r3);

        // Session 4: Excused
        var s4 = Session.Create(_organizationId, _courseGroup.Id, DateTime.UtcNow.AddDays(4), DateTime.UtcNow.AddDays(4).AddHours(1));
        _dbContext.Sessions.Add(s4);
        var r4 = AttendanceRecord.Create(_organizationId, s4.Id, _student.Id, AttendanceConstants.Statuses.Excused);
        _dbContext.AttendanceRecords.Add(r4);

        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.GetPercentageAsync(_student.Id, _courseGroup.Id, null, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.StudentId.Should().Be(_student.Id);
        result.StudentCode.Should().Be("STU-001");
        result.StudentName.Should().Be("Alice Wonder");
        result.CourseGroupId.Should().Be(_courseGroup.Id);
        result.CourseGroupName.Should().Be("Cohort A");
        result.TotalRecords.Should().Be(4);
        result.AttendedCount.Should().Be(2); // Present + Late
        result.PresentCount.Should().Be(1);
        result.LateCount.Should().Be(1);
        result.AbsentCount.Should().Be(1);
        result.ExcusedCount.Should().Be(1);
        result.AttendancePercentage.Should().Be(50.00m); // (2 / 4) * 100 = 50.00
    }

    [Fact]
    public async Task GetPercentageAsync_WhenZeroRecords_ReturnsZeroPercentage()
    {
        // Act
        var result = await _service.GetPercentageAsync(_student.Id, _courseGroup.Id, null, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.TotalRecords.Should().Be(0);
        result.AttendedCount.Should().Be(0);
        result.AttendancePercentage.Should().Be(0.00m);
    }

    [Fact]
    public async Task GetPercentageAsync_WithoutOrganizationContext_ThrowsBadRequestException()
    {
        // Arrange
        _currentOrgServiceMock.Setup(s => s.OrganizationId).Returns((Guid?)null);

        // Act
        var act = () => _service.GetPercentageAsync(_student.Id, null, null, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<BadRequestException>()
            .WithMessage("*Organization context is required*");
    }

    #endregion

    #region Caller Authorization Filtering Tests

    [Fact]
    public async Task GetPagedAsync_WhenCallerIsStudent_FiltersToStudentRecordsOnly()
    {
        // Arrange
        var user2 = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            OrganizationId = _organizationId,
            UserName = "stu2@t.com",
            FirstName = "Jane",
            LastName = "Smith"
        };
        _dbContext.ApplicationUsers.Add(user2);
        var student2 = Student.Create(user2.Id, _organizationId, "STU-002");
        _dbContext.Students.Add(student2);

        var r1 = AttendanceRecord.Create(_organizationId, _session.Id, _student.Id);
        var r2 = AttendanceRecord.Create(_organizationId, _session.Id, student2.Id);
        _dbContext.AttendanceRecords.AddRange(r1, r2);
        await _dbContext.SaveChangesAsync();

        _validatorMock.Setup(v => v.ResolveCallerPermissionsAsync(_studentUser.Id, _organizationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CallerPermissions(false, null, _student));

        // Act
        var result = await _service.GetPagedAsync(new AttendanceQueryParameters(), _studentUser.Id, CancellationToken.None);

        // Assert
        result.Items.Should().HaveCount(1);
        result.Items[0].StudentId.Should().Be(_student.Id);
    }

    [Fact]
    public async Task GetPagedAsync_WhenCallerIsStudentAndRequestsDifferentStudent_ThrowsForbiddenAccessException()
    {
        // Arrange
        _validatorMock.Setup(v => v.ResolveCallerPermissionsAsync(_studentUser.Id, _organizationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CallerPermissions(false, null, _student));

        var query = new AttendanceQueryParameters { StudentId = Guid.NewGuid() };

        // Act
        var act = () => _service.GetPagedAsync(query, _studentUser.Id, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ForbiddenAccessException>()
            .WithMessage("*Students can only view their own attendance records*");
    }

    #endregion

    #region Batch Attendance Service Tests (Phase 3.2)

    [Fact]
    public async Task SaveBatchAsync_SuccessfulCreation_PersistsAllRecordsAndReturnsDtos()
    {
        // Arrange: Create second student
        var user2 = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            OrganizationId = _organizationId,
            UserName = "stu2_batch@t.com",
            FirstName = "Student",
            LastName = "Two"
        };
        _dbContext.ApplicationUsers.Add(user2);
        var student2 = Student.Create(user2.Id, _organizationId, "STU-002");
        _dbContext.Students.Add(student2);
        await _dbContext.SaveChangesAsync();

        var dto = new BatchAttendanceRequestDto
        {
            Records =
            [
                new BatchAttendanceItemDto { StudentId = _student.Id, Status = AttendanceConstants.Statuses.Present, Notes = "Note 1" },
                new BatchAttendanceItemDto { StudentId = student2.Id, Status = AttendanceConstants.Statuses.Late, Notes = "Note 2" }
            ]
        };

        // Act
        var result = await _service.SaveBatchAsync(_session.Id, dto, _studentUser.Id);

        // Assert
        result.Should().HaveCount(2);
        result.Should().ContainSingle(r => r.StudentId == _student.Id && r.Status == AttendanceConstants.Statuses.Present && r.Notes == "Note 1");
        result.Should().ContainSingle(r => r.StudentId == student2.Id && r.Status == AttendanceConstants.Statuses.Late && r.Notes == "Note 2");

        var dbRecords = _dbContext.AttendanceRecords.Where(a => a.SessionId == _session.Id).ToList();
        dbRecords.Should().HaveCount(2);
    }

    [Fact]
    public async Task SaveBatchAsync_UpdatingExistingRecords_UpdatesWithoutCreatingDuplicates()
    {
        // Arrange: Pre-seed existing attendance record
        var existing = AttendanceRecord.Create(_organizationId, _session.Id, _student.Id, AttendanceConstants.Statuses.Absent, notes: "Initial Absent");
        _dbContext.AttendanceRecords.Add(existing);
        await _dbContext.SaveChangesAsync();

        var dto = new BatchAttendanceRequestDto
        {
            Records =
            [
                new BatchAttendanceItemDto { StudentId = _student.Id, Status = AttendanceConstants.Statuses.Present, Notes = "Updated Present" }
            ]
        };

        // Act
        var result = await _service.SaveBatchAsync(_session.Id, dto, _studentUser.Id);

        // Assert: updated in-place without duplicate
        result.Should().HaveCount(1);
        result[0].Id.Should().Be(existing.Id);
        result[0].Status.Should().Be(AttendanceConstants.Statuses.Present);
        result[0].Notes.Should().Be("Updated Present");

        var dbCount = _dbContext.AttendanceRecords.Count(a => a.SessionId == _session.Id && a.StudentId == _student.Id);
        dbCount.Should().Be(1);
    }

    [Fact]
    public async Task SaveBatchAsync_MixedCreateAndUpdate_HandlesBothCorrectly()
    {
        // Arrange: 1 existing, 1 new
        var existing = AttendanceRecord.Create(_organizationId, _session.Id, _student.Id, AttendanceConstants.Statuses.Absent);
        _dbContext.AttendanceRecords.Add(existing);

        var user2 = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            OrganizationId = _organizationId,
            UserName = "stu_mixed@t.com",
            FirstName = "Mixed",
            LastName = "Student"
        };
        _dbContext.ApplicationUsers.Add(user2);
        var student2 = Student.Create(user2.Id, _organizationId, "STU-MIXED");
        _dbContext.Students.Add(student2);
        await _dbContext.SaveChangesAsync();

        var dto = new BatchAttendanceRequestDto
        {
            Records =
            [
                new BatchAttendanceItemDto { StudentId = _student.Id, Status = AttendanceConstants.Statuses.Present, Notes = "Now Present" },
                new BatchAttendanceItemDto { StudentId = student2.Id, Status = AttendanceConstants.Statuses.Excused, Notes = "Doctor note" }
            ]
        };

        // Act
        var result = await _service.SaveBatchAsync(_session.Id, dto, _studentUser.Id);

        // Assert
        result.Should().HaveCount(2);
        result.Should().ContainSingle(r => r.StudentId == _student.Id && r.Status == AttendanceConstants.Statuses.Present);
        result.Should().ContainSingle(r => r.StudentId == student2.Id && r.Status == AttendanceConstants.Statuses.Excused);

        var allRecords = _dbContext.AttendanceRecords.Where(a => a.SessionId == _session.Id).ToList();
        allRecords.Should().HaveCount(2);
    }

    [Fact]
    public async Task SaveBatchAsync_WhenNoOrganizationContext_ThrowsBadRequestException()
    {
        // Arrange
        _currentOrgServiceMock.Setup(s => s.OrganizationId).Returns((Guid?)null);
        var dto = new BatchAttendanceRequestDto
        {
            Records = [new BatchAttendanceItemDto { StudentId = _student.Id, Status = AttendanceConstants.Statuses.Present }]
        };

        // Act
        var act = () => _service.SaveBatchAsync(_session.Id, dto, _studentUser.Id);

        // Assert
        await act.Should().ThrowAsync<BadRequestException>()
            .WithMessage("*Organization context is required*");
    }

    [Fact]
    public async Task SaveBatchAsync_WhenValidationFails_ThrowsAndLeavesNoPartialWrites()
    {
        // Arrange
        var dto = new BatchAttendanceRequestDto
        {
            Records = [new BatchAttendanceItemDto { StudentId = _student.Id, Status = AttendanceConstants.Statuses.Present }]
        };

        _validatorMock.Setup(v => v.ValidateBatchAsync(_session.Id, dto, _organizationId, _studentUser.Id, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ConflictException("Validation failed"));

        // Act
        var act = () => _service.SaveBatchAsync(_session.Id, dto, _studentUser.Id);

        // Assert
        await act.Should().ThrowAsync<ConflictException>();

        var count = _dbContext.AttendanceRecords.Count(a => a.SessionId == _session.Id);
        count.Should().Be(0);
    }

    [Fact]
    public async Task SaveBatchAsync_RepeatedRequests_IsIdempotentAndUpdatesCleanly()
    {
        // Arrange
        var dto = new BatchAttendanceRequestDto
        {
            Records = [new BatchAttendanceItemDto { StudentId = _student.Id, Status = AttendanceConstants.Statuses.Present, Notes = "First save" }]
        };

        // Act: Save once
        var firstResult = await _service.SaveBatchAsync(_session.Id, dto, _studentUser.Id);

        // Update note and save again
        dto.Records[0].Notes = "Second save";
        var secondResult = await _service.SaveBatchAsync(_session.Id, dto, _studentUser.Id);

        // Assert: Exactly 1 record remains in DB, updated with latest notes
        secondResult.Should().HaveCount(1);
        secondResult[0].Id.Should().Be(firstResult[0].Id);
        secondResult[0].Notes.Should().Be("Second save");

        var dbCount = _dbContext.AttendanceRecords.Count(a => a.SessionId == _session.Id && a.StudentId == _student.Id);
        dbCount.Should().Be(1);
    }

    [Fact]
    public async Task SaveBatchAsync_WithActiveTransaction_CommitsAndDisposesTransactionOnSuccess()
    {
        // Arrange: Transaction mock to verify commit and disposal lifecycle
        var mockTx = new Mock<IDbContextTransaction>();
        mockTx.Setup(t => t.CommitAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        mockTx.Setup(t => t.DisposeAsync()).Returns(ValueTask.CompletedTask);

        var testService = new TestableAttendanceService(
            _dbContext,
            _validatorMock.Object,
            _currentOrgServiceMock.Object,
            _loggerMock.Object,
            transaction: mockTx.Object);

        var dto = new BatchAttendanceRequestDto
        {
            Records = [new BatchAttendanceItemDto { StudentId = _student.Id, Status = AttendanceConstants.Statuses.Present }]
        };

        // Act
        var result = await testService.SaveBatchAsync(_session.Id, dto, _studentUser.Id);

        // Assert
        result.Should().HaveCount(1);
        mockTx.Verify(t => t.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
        mockTx.Verify(t => t.RollbackAsync(It.IsAny<CancellationToken>()), Times.Never);
        mockTx.Verify(t => t.DisposeAsync(), Times.Once);
    }

    [Fact]
    public async Task SaveBatchAsync_WhenPersistenceFailsBeforeCommit_RollsBackAndDisposesTransaction()
    {
        // Arrange: Transaction mock where commit throws
        var mockTx = new Mock<IDbContextTransaction>();
        mockTx.Setup(t => t.CommitAsync(It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("Commit failed"));
        mockTx.Setup(t => t.RollbackAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        mockTx.Setup(t => t.DisposeAsync()).Returns(ValueTask.CompletedTask);

        var testService = new TestableAttendanceService(
            _dbContext,
            _validatorMock.Object,
            _currentOrgServiceMock.Object,
            _loggerMock.Object,
            transaction: mockTx.Object);

        var dto = new BatchAttendanceRequestDto
        {
            Records = [new BatchAttendanceItemDto { StudentId = _student.Id, Status = AttendanceConstants.Statuses.Present }]
        };

        // Act
        var act = () => testService.SaveBatchAsync(_session.Id, dto, _studentUser.Id);

        // Assert: Rolls back, disposes transaction, and rethrows original exception
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Commit failed");

        mockTx.Verify(t => t.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
        mockTx.Verify(t => t.DisposeAsync(), Times.Once);
    }

    [Fact]
    public async Task SaveBatchAsync_WhenResponseMaterializationFailsAfterCommit_DoesNotRollbackAndDisposesTransaction()
    {
        // Arrange: Successful commit, but response materialization query throws (e.g. read timeout/cancellation)
        var mockTx = new Mock<IDbContextTransaction>();
        mockTx.Setup(t => t.CommitAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        mockTx.Setup(t => t.DisposeAsync()).Returns(ValueTask.CompletedTask);

        var testService = new TestableAttendanceService(
            _dbContext,
            _validatorMock.Object,
            _currentOrgServiceMock.Object,
            _loggerMock.Object,
            transaction: mockTx.Object,
            materializeFunc: () => throw new InvalidOperationException("Materialization read failed"));

        var dto = new BatchAttendanceRequestDto
        {
            Records = [new BatchAttendanceItemDto { StudentId = _student.Id, Status = AttendanceConstants.Statuses.Present }]
        };

        // Act
        var act = () => testService.SaveBatchAsync(_session.Id, dto, _studentUser.Id);

        // Assert: Throws exception, does NOT return fake success, committed transaction is NOT rolled back
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Materialization read failed");

        mockTx.Verify(t => t.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
        mockTx.Verify(t => t.RollbackAsync(It.IsAny<CancellationToken>()), Times.Never);
        mockTx.Verify(t => t.DisposeAsync(), Times.Once);

        // The write was committed and remains in the database
        var dbRecord = _dbContext.AttendanceRecords.FirstOrDefault(a => a.SessionId == _session.Id && a.StudentId == _student.Id);
        dbRecord.Should().NotBeNull();
    }

    [Fact]
    public async Task SaveBatchAsync_WhenRollbackThrows_DoesNotMaskOriginalException()
    {
        // Arrange: Failure before commit, and rollback also fails
        var mockTx = new Mock<IDbContextTransaction>();
        mockTx.Setup(t => t.CommitAsync(It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("Root failure during save"));
        mockTx.Setup(t => t.RollbackAsync(It.IsAny<CancellationToken>())).ThrowsAsync(new Exception("Network lost during rollback"));
        mockTx.Setup(t => t.DisposeAsync()).Returns(ValueTask.CompletedTask);

        var testService = new TestableAttendanceService(
            _dbContext,
            _validatorMock.Object,
            _currentOrgServiceMock.Object,
            _loggerMock.Object,
            transaction: mockTx.Object);

        var dto = new BatchAttendanceRequestDto
        {
            Records = [new BatchAttendanceItemDto { StudentId = _student.Id, Status = AttendanceConstants.Statuses.Present }]
        };

        // Act
        var act = () => testService.SaveBatchAsync(_session.Id, dto, _studentUser.Id);

        // Assert: Root exception is preserved, not masked by rollback failure
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Root failure during save");

        mockTx.Verify(t => t.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
        mockTx.Verify(t => t.DisposeAsync(), Times.Once);
    }

    [Fact]
    public async Task SaveBatchAsync_WhenEmptyRecords_ThrowsValidationExceptionWithoutStartingTransaction()
    {
        // Arrange
        var mockTx = new Mock<IDbContextTransaction>();

        var testService = new TestableAttendanceService(
            _dbContext,
            _validatorMock.Object,
            _currentOrgServiceMock.Object,
            _loggerMock.Object,
            transaction: mockTx.Object);

        var dto = new BatchAttendanceRequestDto
        {
            Records = []
        };

        // Act
        var act = () => testService.SaveBatchAsync(_session.Id, dto, _studentUser.Id);

        // Assert
        await act.Should().ThrowAsync<ValidationException>();
        mockTx.Verify(t => t.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
        mockTx.Verify(t => t.RollbackAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    #endregion

    private class TestableAttendanceService : AttendanceService
    {
        private readonly IDbContextTransaction? _transaction;
        private readonly Func<Task<IReadOnlyList<AttendanceDto>>>? _materializeFunc;

        public TestableAttendanceService(
            ITrainHubDbContext dbContext,
            IAttendanceValidator validator,
            ICurrentOrganizationService orgService,
            ILogger<AttendanceService> logger,
            IDbContextTransaction? transaction = null,
            Func<Task<IReadOnlyList<AttendanceDto>>>? materializeFunc = null)
            : base(dbContext, validator, orgService, logger)
        {
            _transaction = transaction;
            _materializeFunc = materializeFunc;
        }

        protected override Task<IDbContextTransaction?> BeginTransactionAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(_transaction);
        }

        protected override Task<IReadOnlyList<AttendanceDto>> MaterializeBatchResponseAsync(
            Guid sessionId,
            Guid organizationId,
            List<Guid> studentIds,
            CancellationToken cancellationToken)
        {
            if (_materializeFunc != null)
            {
                return _materializeFunc();
            }
            return base.MaterializeBatchResponseAsync(sessionId, organizationId, studentIds, cancellationToken);
        }
    }
}

