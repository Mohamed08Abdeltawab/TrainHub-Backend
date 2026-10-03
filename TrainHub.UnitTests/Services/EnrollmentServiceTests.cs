using FluentAssertions;
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
}
