using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using TrainHub.Application.Common.Exceptions;
using TrainHub.Application.Features.CourseGroups.DTOs;
using TrainHub.Application.Interfaces;
using TrainHub.Application.Services;
using TrainHub.Domain.Constants;
using TrainHub.Domain.Models;
using TrainHub.Infrastructure.Data;
using TrainHub.UnitTests.Common;

namespace TrainHub.UnitTests.Services;

public class CourseGroupServiceTests : IDisposable
{
    private readonly TrainHubDbContext _dbContext;
    private readonly Mock<ICourseGroupValidator> _validatorMock;
    private readonly Mock<ICurrentOrganizationService> _currentOrgServiceMock;
    private readonly Mock<ILogger<CourseGroupService>> _loggerMock;
    private readonly CourseGroupService _service;
    private readonly Guid _organizationId = Guid.NewGuid();
    private readonly Course _course;
    private readonly Instructor _instructor;
    private readonly ApplicationUser _user;

    public CourseGroupServiceTests()
    {
        _dbContext = TestHelpers.CreateInMemoryDbContext();
        _validatorMock = new Mock<ICourseGroupValidator>();
        _currentOrgServiceMock = new Mock<ICurrentOrganizationService>();
        _currentOrgServiceMock.Setup(s => s.OrganizationId).Returns(_organizationId);
        _loggerMock = new Mock<ILogger<CourseGroupService>>();

        _service = new CourseGroupService(
            _dbContext,
            _validatorMock.Object,
            _currentOrgServiceMock.Object,
            _loggerMock.Object);

        // Seed org
        var org = new Organization
        {
            Id = _organizationId,
            Name = "Main Org",
            Slug = "main-org",
            IsActive = true
        };
        _dbContext.Organizations.Add(org);

        // Seed course
        _course = Course.Create(_organizationId, "C# Web API");
        _dbContext.Courses.Add(_course);

        // Seed user and instructor
        _user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            OrganizationId = _organizationId,
            FirstName = "John",
            LastName = "Doe",
            Email = "john.doe@example.com",
            UserName = "johndoe"
        };
        _dbContext.ApplicationUsers.Add(_user);

        _instructor = Instructor.Create(_user.Id, _organizationId, "DotNet");
        _dbContext.Instructors.Add(_instructor);

        _dbContext.SaveChanges();
    }

    public void Dispose()
    {
        _dbContext.Database.EnsureDeleted();
        _dbContext.Dispose();
    }

    #region CreateAsync

    [Fact]
    public async Task CreateAsync_ValidDto_CreatesAndReturnsCourseGroupDto()
    {
        // Arrange
        var dto = new CreateCourseGroupDto
        {
            CourseId = _course.Id,
            InstructorId = _instructor.Id,
            Name = "Cohort 1",
            StartDate = new DateOnly(2026, 10, 1),
            EndDate = new DateOnly(2026, 12, 1),
            ScheduleDescription = "Mon & Wed 6-8 PM",
            Capacity = 25
        };

        // Act
        var result = await _service.CreateAsync(dto, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().NotBeEmpty();
        result.OrganizationId.Should().Be(_organizationId);
        result.CourseId.Should().Be(_course.Id);
        result.CourseName.Should().Be(_course.Name);
        result.InstructorId.Should().Be(_instructor.Id);
        result.InstructorName.Should().Be("John Doe");
        result.Name.Should().Be("Cohort 1");
        result.StartDate.Should().Be(new DateOnly(2026, 10, 1));
        result.EndDate.Should().Be(new DateOnly(2026, 12, 1));
        result.ScheduleDescription.Should().Be("Mon & Wed 6-8 PM");
        result.Capacity.Should().Be(25);
        result.Status.Should().Be(CourseGroupConstants.DefaultStatus);

        _validatorMock.Verify(v => v.ValidateCreateAsync(dto, _organizationId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_WithoutOrgContext_ThrowsBadRequestException()
    {
        // Arrange
        _currentOrgServiceMock.Setup(s => s.OrganizationId).Returns((Guid?)null);
        var dto = new CreateCourseGroupDto { CourseId = _course.Id, Name = "Batch 1" };

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
    public async Task GetByIdAsync_WhenExists_ReturnsCourseGroupDto()
    {
        // Arrange
        var group = CourseGroup.Create(
            _organizationId,
            _course.Id,
            "Cohort A",
            new DateOnly(2026, 10, 1),
            new DateOnly(2026, 12, 1),
            "Schedule",
            _instructor.Id);
        _dbContext.CourseGroups.Add(group);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.GetByIdAsync(group.Id, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(group.Id);
        result.Name.Should().Be("Cohort A");
        result.CourseName.Should().Be(_course.Name);
        result.InstructorName.Should().Be("John Doe");
    }

    [Fact]
    public async Task GetByIdAsync_WhenNotExists_ThrowsNotFoundException()
    {
        // Act
        var act = () => _service.GetByIdAsync(Guid.NewGuid(), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("*CourseGroup*");
    }

    #endregion

    #region GetPagedAsync

    [Fact]
    public async Task GetPagedAsync_ReturnsPagedResults()
    {
        // Arrange
        var g1 = CourseGroup.Create(_organizationId, _course.Id, "Group Alpha", new DateOnly(2026, 10, 1), new DateOnly(2026, 12, 1));
        var g2 = CourseGroup.Create(_organizationId, _course.Id, "Group Beta", new DateOnly(2026, 10, 5), new DateOnly(2026, 12, 5));
        _dbContext.CourseGroups.AddRange(g1, g2);
        await _dbContext.SaveChangesAsync();

        var query = new CourseGroupQueryParameters { PageNumber = 1, PageSize = 10 };

        // Act
        var result = await _service.GetPagedAsync(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.TotalCount.Should().Be(2);
        result.Items.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetPagedAsync_WithSearchTerm_FiltersCorrectly()
    {
        // Arrange
        var g1 = CourseGroup.Create(_organizationId, _course.Id, "Group Alpha", new DateOnly(2026, 10, 1), new DateOnly(2026, 12, 1), "Evening");
        var g2 = CourseGroup.Create(_organizationId, _course.Id, "Group Beta", new DateOnly(2026, 10, 5), new DateOnly(2026, 12, 5), "Morning");
        _dbContext.CourseGroups.AddRange(g1, g2);
        await _dbContext.SaveChangesAsync();

        var query = new CourseGroupQueryParameters { SearchTerm = "Alpha" };

        // Act
        var result = await _service.GetPagedAsync(query, CancellationToken.None);

        // Assert
        result.TotalCount.Should().Be(1);
        result.Items.First().Name.Should().Be("Group Alpha");
    }

    [Fact]
    public async Task GetPagedAsync_WithCourseFilter_FiltersCorrectly()
    {
        // Arrange
        var course2 = Course.Create(_organizationId, "C# Advanced");
        _dbContext.Courses.Add(course2);

        var g1 = CourseGroup.Create(_organizationId, _course.Id, "Group Alpha", new DateOnly(2026, 10, 1), new DateOnly(2026, 12, 1));
        var g2 = CourseGroup.Create(_organizationId, course2.Id, "Group Beta", new DateOnly(2026, 10, 5), new DateOnly(2026, 12, 5));
        _dbContext.CourseGroups.AddRange(g1, g2);
        await _dbContext.SaveChangesAsync();

        var query = new CourseGroupQueryParameters { CourseId = course2.Id };

        // Act
        var result = await _service.GetPagedAsync(query, CancellationToken.None);

        // Assert
        result.TotalCount.Should().Be(1);
        result.Items.First().Name.Should().Be("Group Beta");
    }

    #endregion

    #region UpdateAsync

    [Fact]
    public async Task UpdateAsync_ValidDto_UpdatesAndReturnsDto()
    {
        // Arrange
        var group = CourseGroup.Create(
            _organizationId,
            _course.Id,
            "Initial Name",
            new DateOnly(2026, 10, 1),
            new DateOnly(2026, 12, 1));
        _dbContext.CourseGroups.Add(group);
        await _dbContext.SaveChangesAsync();

        var dto = new UpdateCourseGroupDto
        {
            Name = "Updated Name",
            StartDate = new DateOnly(2026, 10, 15),
            EndDate = new DateOnly(2026, 12, 15),
            ScheduleDescription = "Mon 6 PM",
            InstructorId = _instructor.Id,
            Capacity = 35
        };

        // Act
        var result = await _service.UpdateAsync(group.Id, dto, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Name.Should().Be("Updated Name");
        result.StartDate.Should().Be(new DateOnly(2026, 10, 15));
        result.EndDate.Should().Be(new DateOnly(2026, 12, 15));
        result.ScheduleDescription.Should().Be("Mon 6 PM");
        result.InstructorId.Should().Be(_instructor.Id);
        result.InstructorName.Should().Be("John Doe");
        result.Capacity.Should().Be(35);

        _validatorMock.Verify(v => v.ValidateUpdateAsync(group.Id, dto, _organizationId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_NonExistentGroup_ThrowsNotFoundException()
    {
        // Arrange
        var dto = new UpdateCourseGroupDto
        {
            Name = "Updated",
            StartDate = new DateOnly(2026, 10, 1),
            EndDate = new DateOnly(2026, 12, 1)
        };

        // Act
        var act = () => _service.UpdateAsync(Guid.NewGuid(), dto, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("*CourseGroup*");
    }

    #endregion

    #region DeleteAsync

    [Fact]
    public async Task DeleteAsync_ExistingGroup_DeletesSuccessfully()
    {
        // Arrange
        var group = CourseGroup.Create(
            _organizationId,
            _course.Id,
            "To Delete",
            new DateOnly(2026, 10, 1),
            new DateOnly(2026, 12, 1));
        _dbContext.CourseGroups.Add(group);
        await _dbContext.SaveChangesAsync();

        // Act
        await _service.DeleteAsync(group.Id, CancellationToken.None);

        // Assert
        var deleted = await _dbContext.CourseGroups.FindAsync(group.Id);
        deleted.Should().BeNull();

        _validatorMock.Verify(v => v.ValidateDeleteAsync(group.Id, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteAsync_NonExistentGroup_ThrowsNotFoundException()
    {
        // Act
        var act = () => _service.DeleteAsync(Guid.NewGuid(), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("*CourseGroup*");
    }

    #endregion
}
