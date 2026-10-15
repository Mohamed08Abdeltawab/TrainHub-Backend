using FluentAssertions;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using Moq;
using TrainHub.Application.Common.Exceptions;
using TrainHub.Application.Features.Enrollments.DTOs;
using TrainHub.Application.Interfaces;
using TrainHub.Application.Services;
using TrainHub.Domain.Constants;
using TrainHub.Domain.Models;
using TrainHub.Infrastructure.Data;
using TrainHub.UnitTests.Common;

namespace TrainHub.UnitTests.Services;

public class EnrollmentServiceTests : IDisposable
{
    private readonly TrainHubDbContext _dbContext;
    private readonly Mock<IEnrollmentValidator> _validatorMock;
    private readonly Mock<ICurrentOrganizationService> _currentOrgServiceMock;
    private readonly Mock<ILogger<EnrollmentService>> _loggerMock;
    private readonly EnrollmentService _service;
    private readonly Guid _organizationId = Guid.NewGuid();
    private readonly CourseGroup _courseGroup;
    private readonly Student _student;
    private readonly ApplicationUser _studentUser;

    public EnrollmentServiceTests()
    {
        _dbContext = TestHelpers.CreateInMemoryDbContext();
        _validatorMock = new Mock<IEnrollmentValidator>();
        _currentOrgServiceMock = new Mock<ICurrentOrganizationService>();
        _currentOrgServiceMock.Setup(s => s.OrganizationId).Returns(_organizationId);
        _loggerMock = new Mock<ILogger<EnrollmentService>>();

        _service = new EnrollmentService(
            _dbContext,
            _validatorMock.Object,
            _currentOrgServiceMock.Object,
            _loggerMock.Object);

        // Seed organization
        var org = new Organization
        {
            Id = _organizationId,
            Name = "Main Org",
            Slug = "main-org",
            IsActive = true
        };
        _dbContext.Organizations.Add(org);

        // Seed course & course group
        var course = Course.Create(_organizationId, "C# Web API");
        _dbContext.Courses.Add(course);

        _courseGroup = CourseGroup.Create(
            _organizationId,
            course.Id,
            "Group A",
            new DateOnly(2026, 10, 1),
            new DateOnly(2026, 12, 1));
        _dbContext.CourseGroups.Add(_courseGroup);

        // Seed student user and student
        _studentUser = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            OrganizationId = _organizationId,
            FirstName = "Alice",
            LastName = "Smith",
            Email = "alice@example.com",
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

    #region CreateAsync

    [Fact]
    public async Task CreateAsync_ValidDto_CreatesAndReturnsDto()
    {
        // Arrange
        var dto = new CreateEnrollmentDto
        {
            CourseGroupId = _courseGroup.Id,
            StudentId = _student.Id,
            PaidAmount = 150m,
            Status = EnrollmentConstants.Statuses.Active
        };

        // Act
        var result = await _service.CreateAsync(dto, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.CourseGroupId.Should().Be(_courseGroup.Id);
        result.StudentId.Should().Be(_student.Id);
        result.PaidAmount.Should().Be(150m);
        result.Status.Should().Be(EnrollmentConstants.Statuses.Active);
        result.StudentCode.Should().Be("STU-001");
        result.StudentName.Should().Be("Alice Smith");

        _validatorMock.Verify(v => v.ValidateCreateAsync(dto, _organizationId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_WithoutOrganizationContext_ThrowsBadRequestException()
    {
        // Arrange
        _currentOrgServiceMock.Setup(s => s.OrganizationId).Returns((Guid?)null);
        var dto = new CreateEnrollmentDto
        {
            CourseGroupId = _courseGroup.Id,
            StudentId = _student.Id
        };

        // Act
        var act = () => _service.CreateAsync(dto, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<BadRequestException>()
            .WithMessage("*Organization context is required*");
    }

    [Fact]
    public async Task CreateAsync_NullDto_ThrowsArgumentNullException()
    {
        // Act
        var act = () => _service.CreateAsync(null!, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    #endregion

    #region GetByIdAsync

    [Fact]
    public async Task GetByIdAsync_ExistingId_ReturnsDto()
    {
        // Arrange
        var enrollment = Enrollment.Create(_organizationId, _courseGroup.Id, _student.Id, paidAmount: 200m);
        _dbContext.Enrollments.Add(enrollment);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.GetByIdAsync(enrollment.Id, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(enrollment.Id);
        result.CourseGroupName.Should().Be("Group A");
        result.StudentCode.Should().Be("STU-001");
        result.PaidAmount.Should().Be(200m);
    }

    [Fact]
    public async Task GetByIdAsync_NonExistingId_ThrowsNotFoundException()
    {
        // Act
        var act = () => _service.GetByIdAsync(Guid.NewGuid(), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
    }

    #endregion

    #region GetPagedAsync

    [Fact]
    public async Task GetPagedAsync_ReturnsPagedResult()
    {
        // Arrange
        var enrollment1 = Enrollment.Create(_organizationId, _courseGroup.Id, _student.Id);
        _dbContext.Enrollments.Add(enrollment1);
        await _dbContext.SaveChangesAsync();

        var query = new EnrollmentQueryParameters();

        // Act
        var result = await _service.GetPagedAsync(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.TotalCount.Should().Be(1);
        result.Items.Should().HaveCount(1);
        _validatorMock.Verify(v => v.ValidateQuery(query), Times.Once);
    }

    [Fact]
    public async Task GetPagedAsync_WithFilters_FiltersCorrectly()
    {
        // Arrange
        var otherStudentUser = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            OrganizationId = _organizationId,
            FirstName = "Bob",
            LastName = "Jones",
            IsActive = true
        };
        _dbContext.ApplicationUsers.Add(otherStudentUser);
        var otherStudent = Student.Create(otherStudentUser.Id, _organizationId, "STU-002");
        _dbContext.Students.Add(otherStudent);

        var enrollment1 = Enrollment.Create(_organizationId, _courseGroup.Id, _student.Id, status: EnrollmentConstants.Statuses.Active);
        var enrollment2 = Enrollment.Create(_organizationId, _courseGroup.Id, otherStudent.Id, status: EnrollmentConstants.Statuses.Cancelled);
        _dbContext.Enrollments.AddRange(enrollment1, enrollment2);
        await _dbContext.SaveChangesAsync();

        var query = new EnrollmentQueryParameters
        {
            StudentId = otherStudent.Id,
            Status = EnrollmentConstants.Statuses.Cancelled
        };

        // Act
        var result = await _service.GetPagedAsync(query, CancellationToken.None);

        // Assert
        result.TotalCount.Should().Be(1);
        result.Items.First().StudentId.Should().Be(otherStudent.Id);
        result.Items.First().Status.Should().Be(EnrollmentConstants.Statuses.Cancelled);
    }

    #endregion

    #region UpdateAsync

    [Fact]
    public async Task UpdateAsync_ValidDto_UpdatesAndReturnsDto()
    {
        // Arrange
        var enrollment = Enrollment.Create(_organizationId, _courseGroup.Id, _student.Id, paidAmount: 100m);
        _dbContext.Enrollments.Add(enrollment);
        await _dbContext.SaveChangesAsync();

        var dto = new UpdateEnrollmentDto
        {
            PaidAmount = 300m,
            Status = EnrollmentConstants.Statuses.Completed
        };

        // Act
        var result = await _service.UpdateAsync(enrollment.Id, dto, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.PaidAmount.Should().Be(300m);
        result.Status.Should().Be(EnrollmentConstants.Statuses.Completed);

        _validatorMock.Verify(v => v.ValidateUpdateAsync(enrollment.Id, dto, _organizationId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_NonExistingId_ThrowsNotFoundException()
    {
        // Arrange
        var dto = new UpdateEnrollmentDto { PaidAmount = 100m };

        // Act
        var act = () => _service.UpdateAsync(Guid.NewGuid(), dto, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
    }

    #endregion

    #region UpdateStatusAsync

    [Fact]
    public async Task UpdateStatusAsync_ValidDto_UpdatesStatus()
    {
        // Arrange
        var enrollment = Enrollment.Create(_organizationId, _courseGroup.Id, _student.Id, status: EnrollmentConstants.Statuses.Active);
        _dbContext.Enrollments.Add(enrollment);
        await _dbContext.SaveChangesAsync();

        var dto = new UpdateEnrollmentStatusDto { Status = EnrollmentConstants.Statuses.Completed };

        // Act
        var result = await _service.UpdateStatusAsync(enrollment.Id, dto, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(EnrollmentConstants.Statuses.Completed);
        _validatorMock.Verify(v => v.ValidateUpdateStatusAsync(enrollment.Id, dto.Status, It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region DeleteAsync

    [Fact]
    public async Task DeleteAsync_ExistingId_DeletesEnrollment()
    {
        // Arrange
        var enrollment = Enrollment.Create(_organizationId, _courseGroup.Id, _student.Id);
        _dbContext.Enrollments.Add(enrollment);
        await _dbContext.SaveChangesAsync();

        // Act
        await _service.DeleteAsync(enrollment.Id, CancellationToken.None);

        // Assert
        _dbContext.Enrollments.Find(enrollment.Id).Should().BeNull();
        _validatorMock.Verify(v => v.ValidateDeleteAsync(enrollment.Id, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteAsync_NonExistingId_ThrowsNotFoundException()
    {
        // Act
        var act = () => _service.DeleteAsync(Guid.NewGuid(), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
    }

    #endregion

    #region DAT-01: Group Capacity Concurrency & Race Condition Tests

    [Fact]
    public async Task CreateAsync_WhenGroupCapacityReachedWithinSynchronizationScope_ThrowsConflictException()
    {
        // Arrange: Create a group with Capacity = 1
        var smallGroup = CourseGroup.Create(
            _organizationId,
            _courseGroup.CourseId,
            "Small Group",
            new DateOnly(2026, 10, 1),
            new DateOnly(2026, 12, 1),
            capacity: 1);
        await _dbContext.CourseGroups.AddAsync(smallGroup);

        // Pre-fill with 1 active enrollment (reaching capacity)
        var existingEnrollment = Enrollment.Create(_organizationId, smallGroup.Id, _student.Id, status: EnrollmentConstants.Statuses.Active);
        await _dbContext.Enrollments.AddAsync(existingEnrollment);

        // Second student
        var user2 = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            OrganizationId = _organizationId,
            FirstName = "Bob",
            LastName = "Jones",
            Email = "bob@example.com",
            IsActive = true
        };
        await _dbContext.ApplicationUsers.AddAsync(user2);
        var student2 = Student.Create(user2.Id, _organizationId, "STU-002");
        await _dbContext.Students.AddAsync(student2);
        await _dbContext.SaveChangesAsync();

        var dto = new CreateEnrollmentDto
        {
            CourseGroupId = smallGroup.Id,
            StudentId = student2.Id,
            Status = EnrollmentConstants.Statuses.Active
        };

        // Act
        var act = () => _service.CreateAsync(dto);

        // Assert
        await act.Should().ThrowAsync<ConflictException>()
            .WithMessage("*has reached its maximum capacity*");
    }

    [Fact]
    public async Task CreateAsync_WhenStudentAlreadyEnrolledWithinSynchronizationScope_ThrowsConflictException()
    {
        // Arrange
        var existingEnrollment = Enrollment.Create(_organizationId, _courseGroup.Id, _student.Id, status: EnrollmentConstants.Statuses.Active);
        await _dbContext.Enrollments.AddAsync(existingEnrollment);
        await _dbContext.SaveChangesAsync();

        var dto = new CreateEnrollmentDto
        {
            CourseGroupId = _courseGroup.Id,
            StudentId = _student.Id,
            Status = EnrollmentConstants.Statuses.Active
        };

        // Act
        var act = () => _service.CreateAsync(dto);

        // Assert
        await act.Should().ThrowAsync<ConflictException>()
            .WithMessage("*already enrolled in this course group*");
    }

    [Fact]
    public async Task CreateAsync_WhenPreviousEnrollmentWasCancelled_DoesNotCountTowardsCapacity()
    {
        // Arrange: Group with Capacity = 1
        var singleSlotGroup = CourseGroup.Create(
            _organizationId,
            _courseGroup.CourseId,
            "Single Slot",
            new DateOnly(2026, 10, 1),
            new DateOnly(2026, 12, 1),
            capacity: 1);
        await _dbContext.CourseGroups.AddAsync(singleSlotGroup);

        // Pre-fill with a CANCELLED enrollment
        var cancelledEnrollment = Enrollment.Create(_organizationId, singleSlotGroup.Id, _student.Id, status: EnrollmentConstants.Statuses.Cancelled);
        await _dbContext.Enrollments.AddAsync(cancelledEnrollment);

        // New student
        var user2 = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            OrganizationId = _organizationId,
            FirstName = "Carol",
            LastName = "Danvers",
            Email = "carol@example.com",
            IsActive = true
        };
        await _dbContext.ApplicationUsers.AddAsync(user2);
        var student2 = Student.Create(user2.Id, _organizationId, "STU-003");
        await _dbContext.Students.AddAsync(student2);
        await _dbContext.SaveChangesAsync();

        var dto = new CreateEnrollmentDto
        {
            CourseGroupId = singleSlotGroup.Id,
            StudentId = student2.Id,
            Status = EnrollmentConstants.Statuses.Active
        };

        // Act
        var result = await _service.CreateAsync(dto);

        // Assert
        result.Should().NotBeNull();
        result.StudentId.Should().Be(student2.Id);
    }

    [Fact]
    public async Task CreateAsync_ConcurrentEnrollmentsExceedingCapacity_EnforcesCapacityLimit()
    {
        // Arrange: Group with Capacity = 2
        var capacity2Group = CourseGroup.Create(
            _organizationId,
            _courseGroup.CourseId,
            "Capacity 2 Group",
            new DateOnly(2026, 10, 1),
            new DateOnly(2026, 12, 1),
            capacity: 2);
        await _dbContext.CourseGroups.AddAsync(capacity2Group);

        // Create 5 distinct students
        var studentIds = new List<Guid>();
        for (int i = 1; i <= 5; i++)
        {
            var u = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                OrganizationId = _organizationId,
                FirstName = $"Student{i}",
                LastName = "Test",
                Email = $"student{i}@example.com",
                IsActive = true
            };
            await _dbContext.ApplicationUsers.AddAsync(u);
            var s = Student.Create(u.Id, _organizationId, $"STU-C-{i}");
            await _dbContext.Students.AddAsync(s);
            studentIds.Add(s.Id);
        }
        await _dbContext.SaveChangesAsync();

        // Act: Concurrently enroll all 5 students
        var tasks = studentIds.Select(async sId =>
        {
            try
            {
                await _service.CreateAsync(new CreateEnrollmentDto
                {
                    CourseGroupId = capacity2Group.Id,
                    StudentId = sId,
                    Status = EnrollmentConstants.Statuses.Active
                });
                return true;
            }
            catch (ConflictException)
            {
                return false;
            }
        }).ToList();

        var results = await Task.WhenAll(tasks);

        // Assert: Exactly 2 succeed, exactly 3 fail with ConflictException
        var successfulCount = results.Count(r => r);
        var rejectedCount = results.Count(r => !r);

        successfulCount.Should().Be(2);
        rejectedCount.Should().Be(3);

        var dbActiveEnrollments = _dbContext.Enrollments
            .Count(e => e.CourseGroupId == capacity2Group.Id && e.Status != EnrollmentConstants.Statuses.Cancelled);
        dbActiveEnrollments.Should().Be(2);
    }

    [Fact]
    public async Task CreateAsync_WhenSuccessful_CommitsAndDisposesTransaction()
    {
        // Arrange
        var mockTx = new Mock<IDbContextTransaction>();
        mockTx.Setup(t => t.CommitAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        mockTx.Setup(t => t.DisposeAsync()).Returns(ValueTask.CompletedTask);

        var testService = new TestableEnrollmentService(
            _dbContext,
            _validatorMock.Object,
            _currentOrgServiceMock.Object,
            _loggerMock.Object,
            transaction: mockTx.Object);

        var dto = new CreateEnrollmentDto
        {
            CourseGroupId = _courseGroup.Id,
            StudentId = _student.Id,
            Status = EnrollmentConstants.Statuses.Active
        };

        // Act
        var result = await testService.CreateAsync(dto);

        // Assert
        result.Should().NotBeNull();
        mockTx.Verify(t => t.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
        mockTx.Verify(t => t.RollbackAsync(It.IsAny<CancellationToken>()), Times.Never);
        mockTx.Verify(t => t.DisposeAsync(), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_WhenExceptionBeforeCommit_RollsBackAndDisposesTransaction()
    {
        // Arrange
        var mockTx = new Mock<IDbContextTransaction>();
        mockTx.Setup(t => t.CommitAsync(It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("Commit failed"));
        mockTx.Setup(t => t.RollbackAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        mockTx.Setup(t => t.DisposeAsync()).Returns(ValueTask.CompletedTask);

        var testService = new TestableEnrollmentService(
            _dbContext,
            _validatorMock.Object,
            _currentOrgServiceMock.Object,
            _loggerMock.Object,
            transaction: mockTx.Object);

        var dto = new CreateEnrollmentDto
        {
            CourseGroupId = _courseGroup.Id,
            StudentId = _student.Id,
            Status = EnrollmentConstants.Statuses.Active
        };

        // Act
        var act = () => testService.CreateAsync(dto);

        // Assert: Rolls back, disposes transaction, and rethrows original exception
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Commit failed");

        mockTx.Verify(t => t.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
        mockTx.Verify(t => t.DisposeAsync(), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_WhenResponseMaterializationFailsAfterCommit_DoesNotRollbackAndDisposesTransaction()
    {
        // Arrange: Successful commit, but response materialization query throws
        var mockTx = new Mock<IDbContextTransaction>();
        mockTx.Setup(t => t.CommitAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        mockTx.Setup(t => t.DisposeAsync()).Returns(ValueTask.CompletedTask);

        var testService = new TestableEnrollmentService(
            _dbContext,
            _validatorMock.Object,
            _currentOrgServiceMock.Object,
            _loggerMock.Object,
            transaction: mockTx.Object,
            materializeFunc: () => throw new InvalidOperationException("Materialization read failed"));

        var dto = new CreateEnrollmentDto
        {
            CourseGroupId = _courseGroup.Id,
            StudentId = _student.Id,
            Status = EnrollmentConstants.Statuses.Active
        };

        // Act
        var act = () => testService.CreateAsync(dto);

        // Assert: Throws exception, does NOT return fake success, committed transaction is NOT rolled back
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Materialization read failed");

        mockTx.Verify(t => t.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
        mockTx.Verify(t => t.RollbackAsync(It.IsAny<CancellationToken>()), Times.Never);
        mockTx.Verify(t => t.DisposeAsync(), Times.Once);

        // The write was committed and remains in the database
        var dbRecord = _dbContext.Enrollments.FirstOrDefault(e => e.CourseGroupId == _courseGroup.Id && e.StudentId == _student.Id);
        dbRecord.Should().NotBeNull();
    }

    [Fact]
    public async Task CreateAsync_WhenRollbackThrows_DoesNotMaskOriginalException()
    {
        // Arrange: Failure before commit, and rollback also fails
        var mockTx = new Mock<IDbContextTransaction>();
        mockTx.Setup(t => t.CommitAsync(It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("Root failure during save"));
        mockTx.Setup(t => t.RollbackAsync(It.IsAny<CancellationToken>())).ThrowsAsync(new Exception("Network lost during rollback"));
        mockTx.Setup(t => t.DisposeAsync()).Returns(ValueTask.CompletedTask);

        var testService = new TestableEnrollmentService(
            _dbContext,
            _validatorMock.Object,
            _currentOrgServiceMock.Object,
            _loggerMock.Object,
            transaction: mockTx.Object);

        var dto = new CreateEnrollmentDto
        {
            CourseGroupId = _courseGroup.Id,
            StudentId = _student.Id,
            Status = EnrollmentConstants.Statuses.Active
        };

        // Act
        var act = () => testService.CreateAsync(dto);

        // Assert: Root exception is preserved, not masked by rollback failure
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Root failure during save");

        mockTx.Verify(t => t.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
        mockTx.Verify(t => t.DisposeAsync(), Times.Once);
    }

    #endregion

    private class TestableEnrollmentService : EnrollmentService
    {
        private readonly IDbContextTransaction? _transaction;
        private readonly Func<Task<EnrollmentDto>>? _materializeFunc;

        public TestableEnrollmentService(
            ITrainHubDbContext dbContext,
            IEnrollmentValidator validator,
            ICurrentOrganizationService orgService,
            ILogger<EnrollmentService> logger,
            IDbContextTransaction? transaction = null,
            Func<Task<EnrollmentDto>>? materializeFunc = null)
            : base(dbContext, validator, orgService, logger)
        {
            _transaction = transaction;
            _materializeFunc = materializeFunc;
        }

        protected override Task<IDbContextTransaction?> BeginTransactionAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(_transaction);
        }

        protected override Task<EnrollmentDto> MaterializeEnrollmentResponseAsync(Guid enrollmentId, CancellationToken cancellationToken)
        {
            if (_materializeFunc != null)
            {
                return _materializeFunc();
            }
            return base.MaterializeEnrollmentResponseAsync(enrollmentId, cancellationToken);
        }
    }
}
