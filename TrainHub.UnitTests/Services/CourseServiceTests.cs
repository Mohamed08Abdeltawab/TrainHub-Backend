using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using TrainHub.Application.Common.Exceptions;
using TrainHub.Application.Features.Courses.DTOs;
using TrainHub.Application.Interfaces;
using TrainHub.Domain.Constants;
using TrainHub.Domain.Models;
using TrainHub.Infrastructure;
using TrainHub.Infrastructure.Data;
using TrainHub.Application.Services;
using TrainHub.UnitTests.Common;

namespace TrainHub.UnitTests.Services;

public class CourseServiceTests : IDisposable
{
    private readonly TrainHubDbContext _dbContext;
    private readonly Mock<ICourseValidator> _courseValidatorMock;
    private readonly Mock<ICurrentOrganizationService> _currentOrgServiceMock;
    private readonly Mock<ILogger<CourseService>> _loggerMock;
    private readonly CourseService _service;
    private readonly Guid _organizationId = Guid.NewGuid();

    public CourseServiceTests()
    {
        _dbContext = TestHelpers.CreateInMemoryDbContext();
        _courseValidatorMock = new Mock<ICourseValidator>();
        _currentOrgServiceMock = new Mock<ICurrentOrganizationService>();
        _currentOrgServiceMock.Setup(s => s.OrganizationId).Returns(_organizationId);
        _loggerMock = new Mock<ILogger<CourseService>>();

        _service = new CourseService(
            _dbContext,
            _courseValidatorMock.Object,
            _currentOrgServiceMock.Object,
            _loggerMock.Object);
    }

    public void Dispose()
    {
        _dbContext.Database.EnsureDeleted();
        _dbContext.Dispose();
    }

    #region CreateAsync

    [Fact]
    public async Task CreateAsync_ValidDto_CreatesAndReturnsCourseDto()
    {
        // Arrange
        var dto = new CreateCourseDto
        {
            Name = "ASP.NET Core Masterclass",
            Description = "Full stack masterclass",
            Category = "Backend",
            BasePrice = 149.99m,
            DurationInHours = 30,
            ThumbnailUrl = "https://example.com/thumb.png",
            Status = CourseConstants.Statuses.Active
        };

        // Act
        var result = await _service.CreateAsync(dto);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().NotBeEmpty();
        result.OrganizationId.Should().Be(_organizationId);
        result.Name.Should().Be("ASP.NET Core Masterclass");
        result.Category.Should().Be("Backend");
        result.BasePrice.Should().Be(149.99m);
        result.DurationInHours.Should().Be(30);
        result.Status.Should().Be(CourseConstants.Statuses.Active);
        result.StatusName.Should().Be("Active");

        var dbCourse = await _dbContext.Courses.FindAsync(result.Id);
        dbCourse.Should().NotBeNull();
        dbCourse!.Name.Should().Be("ASP.NET Core Masterclass");

        _courseValidatorMock.Verify(v => v.ValidateCreateAsync(dto, _organizationId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_NullDto_ThrowsArgumentNullException()
    {
        // Act
        var act = () => _service.CreateAsync(null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task CreateAsync_WhenNoOrganizationContext_ThrowsBadRequestException()
    {
        // Arrange
        _currentOrgServiceMock.Setup(s => s.OrganizationId).Returns((Guid?)null);
        var dto = new CreateCourseDto { Name = "Test Course" };

        // Act
        var act = () => _service.CreateAsync(dto);

        // Assert
        await act.Should().ThrowAsync<BadRequestException>()
            .WithMessage("*Organization context is required*");
    }

    #endregion

    #region GetByIdAsync

    [Fact]
    public async Task GetByIdAsync_ExistingId_ReturnsCourseDto()
    {
        // Arrange
        var course = Course.Create(_organizationId, "C# Fundamentals", "Learn C# basics", "Programming", 99m);
        _dbContext.Courses.Add(course);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.GetByIdAsync(course.Id);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(course.Id);
        result.Name.Should().Be("C# Fundamentals");
        result.BasePrice.Should().Be(99m);
    }

    [Fact]
    public async Task GetByIdAsync_NonExistentId_ThrowsNotFoundException()
    {
        // Act
        var act = () => _service.GetByIdAsync(Guid.NewGuid());

        // Assert
        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("*Course*");
    }

    #endregion

    #region GetAllAsync

    [Fact]
    public async Task GetAllAsync_ReturnsAllCourses()
    {
        // Arrange
        var c1 = Course.Create(_organizationId, "Course A");
        var c2 = Course.Create(_organizationId, "Course B");
        _dbContext.Courses.AddRange(c1, c2);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.GetAllAsync();

        // Assert
        result.Should().HaveCount(2);
        result.Select(c => c.Name).Should().Contain(["Course A", "Course B"]);
    }

    #endregion

    #region GetPagedAsync

    [Fact]
    public async Task GetPagedAsync_WithSearchTerm_ReturnsMatchingCourses()
    {
        // Arrange
        var c1 = Course.Create(_organizationId, "Docker Deep Dive", "Containers", "DevOps");
        var c2 = Course.Create(_organizationId, "Kubernetes in Action", "Orchestration", "DevOps");
        var c3 = Course.Create(_organizationId, "React Mastery", "Frontend", "Web");
        _dbContext.Courses.AddRange(c1, c2, c3);
        await _dbContext.SaveChangesAsync();

        var query = new CourseQueryParameters { SearchTerm = "Docker" };

        // Act
        var result = await _service.GetPagedAsync(query);

        // Assert
        result.TotalCount.Should().Be(1);
        result.Items.Should().ContainSingle(c => c.Name == "Docker Deep Dive");
    }

    [Fact]
    public async Task GetPagedAsync_WithCategoryFilter_ReturnsMatchingCourses()
    {
        // Arrange
        var c1 = Course.Create(_organizationId, "Course 1", category: "DevOps");
        var c2 = Course.Create(_organizationId, "Course 2", category: "DevOps");
        var c3 = Course.Create(_organizationId, "Course 3", category: "Design");
        _dbContext.Courses.AddRange(c1, c2, c3);
        await _dbContext.SaveChangesAsync();

        var query = new CourseQueryParameters { Category = "DevOps" };

        // Act
        var result = await _service.GetPagedAsync(query);

        // Assert
        result.TotalCount.Should().Be(2);
        result.Items.Should().AllSatisfy(c => c.Category.Should().Be("DevOps"));
    }

    [Fact]
    public async Task GetPagedAsync_WithStatusFilter_ReturnsMatchingCourses()
    {
        // Arrange
        var c1 = Course.Create(_organizationId, "Draft Course", status: CourseConstants.Statuses.Draft);
        var c2 = Course.Create(_organizationId, "Active Course", status: CourseConstants.Statuses.Active);
        _dbContext.Courses.AddRange(c1, c2);
        await _dbContext.SaveChangesAsync();

        var query = new CourseQueryParameters { Status = CourseConstants.Statuses.Draft };

        // Act
        var result = await _service.GetPagedAsync(query);

        // Assert
        result.TotalCount.Should().Be(1);
        result.Items.First().Name.Should().Be("Draft Course");
    }

    [Fact]
    public async Task GetPagedAsync_WithPriceRangeFilter_ReturnsMatchingCourses()
    {
        // Arrange
        var c1 = Course.Create(_organizationId, "Cheap Course", basePrice: 20m);
        var c2 = Course.Create(_organizationId, "Mid Course", basePrice: 100m);
        var c3 = Course.Create(_organizationId, "Expensive Course", basePrice: 500m);
        _dbContext.Courses.AddRange(c1, c2, c3);
        await _dbContext.SaveChangesAsync();

        var query = new CourseQueryParameters { MinPrice = 50m, MaxPrice = 200m };

        // Act
        var result = await _service.GetPagedAsync(query);

        // Assert
        result.TotalCount.Should().Be(1);
        result.Items.First().Name.Should().Be("Mid Course");
    }

    [Fact]
    public async Task GetPagedAsync_WithDurationFilter_ReturnsMatchingCourses()
    {
        // Arrange
        var c1 = Course.Create(_organizationId, "Short Course", durationInHours: 5);
        var c2 = Course.Create(_organizationId, "Long Course", durationInHours: 50);
        _dbContext.Courses.AddRange(c1, c2);
        await _dbContext.SaveChangesAsync();

        var query = new CourseQueryParameters { MinDuration = 20 };

        // Act
        var result = await _service.GetPagedAsync(query);

        // Assert
        result.TotalCount.Should().Be(1);
        result.Items.First().Name.Should().Be("Long Course");
    }

    [Fact]
    public async Task GetPagedAsync_WithSorting_OrdersAscendingAndDescending()
    {
        // Arrange
        var c1 = Course.Create(_organizationId, "Alpha Course", basePrice: 10m);
        var c2 = Course.Create(_organizationId, "Beta Course", basePrice: 50m);
        _dbContext.Courses.AddRange(c1, c2);
        await _dbContext.SaveChangesAsync();

        var queryAsc = new CourseQueryParameters { SortBy = "BasePrice", SortDescending = false };
        var queryDesc = new CourseQueryParameters { SortBy = "BasePrice", SortDescending = true };

        // Act
        var resultAsc = await _service.GetPagedAsync(queryAsc);
        var resultDesc = await _service.GetPagedAsync(queryDesc);

        // Assert
        resultAsc.Items.First().Name.Should().Be("Alpha Course");
        resultDesc.Items.First().Name.Should().Be("Beta Course");
    }

    [Fact]
    public async Task GetPagedAsync_WithPagination_ReturnsCorrectPageSlice()
    {
        // Arrange
        for (int i = 1; i <= 15; i++)
        {
            _dbContext.Courses.Add(Course.Create(_organizationId, $"Course {i:D2}"));
        }
        await _dbContext.SaveChangesAsync();

        var query = new CourseQueryParameters { PageNumber = 2, PageSize = 5 };

        // Act
        var result = await _service.GetPagedAsync(query);

        // Assert
        result.TotalCount.Should().Be(15);
        result.PageNumber.Should().Be(2);
        result.PageSize.Should().Be(5);
        result.TotalPages.Should().Be(3);
        result.Items.Should().HaveCount(5);
    }

    #endregion

    #region UpdateAsync

    [Fact]
    public async Task UpdateAsync_ExistingCourse_UpdatesAndReturnsCourseDto()
    {
        // Arrange
        var course = Course.Create(_organizationId, "Old Name", "Old Desc", "Old Cat", 10m);
        _dbContext.Courses.Add(course);
        await _dbContext.SaveChangesAsync();

        var dto = new UpdateCourseDto
        {
            Name = "New Name",
            Description = "New Desc",
            Category = "New Cat",
            BasePrice = 99m,
            DurationInHours = 15,
            ThumbnailUrl = "https://example.com/new.jpg",
            Status = CourseConstants.Statuses.Inactive
        };

        // Act
        var result = await _service.UpdateAsync(course.Id, dto);

        // Assert
        result.Name.Should().Be("New Name");
        result.Description.Should().Be("New Desc");
        result.Category.Should().Be("New Cat");
        result.BasePrice.Should().Be(99m);
        result.DurationInHours.Should().Be(15);
        result.Status.Should().Be(CourseConstants.Statuses.Inactive);
        result.StatusName.Should().Be("Inactive");

        var updated = await _dbContext.Courses.FindAsync(course.Id);
        updated!.Name.Should().Be("New Name");

        _courseValidatorMock.Verify(v => v.ValidateUpdateAsync(course.Id, dto, _organizationId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_NonExistentCourse_ThrowsNotFoundException()
    {
        // Arrange
        var dto = new UpdateCourseDto { Name = "Valid Name" };

        // Act
        var act = () => _service.UpdateAsync(Guid.NewGuid(), dto);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("*Course*");
    }

    #endregion

    #region DeleteAsync

    [Fact]
    public async Task DeleteAsync_ExistingCourse_RemovesFromDatabase()
    {
        // Arrange
        var course = Course.Create(_organizationId, "To Be Deleted");
        _dbContext.Courses.Add(course);
        await _dbContext.SaveChangesAsync();

        // Act
        await _service.DeleteAsync(course.Id);

        // Assert
        var deleted = await _dbContext.Courses.FindAsync(course.Id);
        deleted.Should().BeNull();

        _courseValidatorMock.Verify(v => v.ValidateDeleteAsync(course.Id, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteAsync_NonExistentCourse_ThrowsNotFoundException()
    {
        // Act
        var act = () => _service.DeleteAsync(Guid.NewGuid());

        // Assert
        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("*Course*");
    }

    #endregion
}
