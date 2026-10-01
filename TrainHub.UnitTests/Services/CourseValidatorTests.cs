using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using TrainHub.Application.Common.Exceptions;
using TrainHub.Application.Features.Courses.DTOs;
using TrainHub.Domain.Constants;
using TrainHub.Domain.Models;
using TrainHub.Infrastructure.Data;
using TrainHub.Application.Services;
using TrainHub.UnitTests.Common;

namespace TrainHub.UnitTests.Services;

public class CourseValidatorTests : IDisposable
{
    private readonly TrainHubDbContext _dbContext;
    private readonly Mock<ILogger<CourseValidator>> _loggerMock;
    private readonly CourseValidator _validator;
    private readonly Guid _organizationId = Guid.NewGuid();

    public CourseValidatorTests()
    {
        _dbContext = TestHelpers.CreateInMemoryDbContext();
        _loggerMock = new Mock<ILogger<CourseValidator>>();
        _validator = new CourseValidator(_dbContext, _loggerMock.Object);

        // Seed test organization
        var org = new Organization
        {
            Id = _organizationId,
            Name = "Test Org",
            Slug = "test-org",
            IsActive = true
        };
        _dbContext.Organizations.Add(org);
        _dbContext.SaveChanges();
    }

    public void Dispose()
    {
        _dbContext.Database.EnsureDeleted();
        _dbContext.Dispose();
    }

    #region ValidateCreateAsync

    [Fact]
    public async Task ValidateCreateAsync_WithValidDto_Succeeds()
    {
        // Arrange
        var dto = new CreateCourseDto
        {
            Name = "Clean Architecture 101",
            Category = "Programming",
            BasePrice = 100m,
            DurationInHours = 20
        };

        // Act
        var act = () => _validator.ValidateCreateAsync(dto, _organizationId, CancellationToken.None);

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ValidateCreateAsync_WhenOrganizationNotFound_ThrowsNotFoundException()
    {
        // Arrange
        var dto = new CreateCourseDto
        {
            Name = "Course 101"
        };
        var nonExistentOrgId = Guid.NewGuid();

        // Act
        var act = () => _validator.ValidateCreateAsync(dto, nonExistentOrgId, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("*Organization*");
    }

    [Fact]
    public async Task ValidateCreateAsync_WhenCourseNameAlreadyExistsInSameOrg_ThrowsConflictException()
    {
        // Arrange
        var existing = Course.Create(_organizationId, "Clean Architecture 101");
        _dbContext.Courses.Add(existing);
        await _dbContext.SaveChangesAsync();

        var dto = new CreateCourseDto
        {
            Name = "  Clean Architecture 101  "
        };

        // Act
        var act = () => _validator.ValidateCreateAsync(dto, _organizationId, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ConflictException>()
            .WithMessage("*already exists in this organization*");
    }

    [Fact]
    public async Task ValidateCreateAsync_WhenCourseNameExistsInDifferentOrg_Succeeds()
    {
        // Arrange
        var otherOrgId = Guid.NewGuid();
        var otherOrg = new Organization
        {
            Id = otherOrgId,
            Name = "Other Org",
            Slug = "other-org",
            IsActive = true
        };
        var existing = Course.Create(otherOrgId, "Clean Architecture 101");
        _dbContext.Organizations.Add(otherOrg);
        _dbContext.Courses.Add(existing);
        await _dbContext.SaveChangesAsync();

        var dto = new CreateCourseDto
        {
            Name = "Clean Architecture 101"
        };

        // Act
        var act = () => _validator.ValidateCreateAsync(dto, _organizationId, CancellationToken.None);

        // Assert
        await act.Should().NotThrowAsync();
    }

    #endregion

    #region ValidateUpdateAsync

    [Fact]
    public async Task ValidateUpdateAsync_WithValidDto_Succeeds()
    {
        // Arrange
        var course = Course.Create(_organizationId, "Original Name");
        _dbContext.Courses.Add(course);
        await _dbContext.SaveChangesAsync();

        var dto = new UpdateCourseDto
        {
            Name = "Updated Name",
            BasePrice = 150m
        };

        // Act
        var act = () => _validator.ValidateUpdateAsync(course.Id, dto, _organizationId, CancellationToken.None);

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ValidateUpdateAsync_WhenCourseNotFound_ThrowsNotFoundException()
    {
        // Arrange
        var dto = new UpdateCourseDto { Name = "Valid Name" };

        // Act
        var act = () => _validator.ValidateUpdateAsync(Guid.NewGuid(), dto, _organizationId, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("*Course*");
    }

    [Fact]
    public async Task ValidateUpdateAsync_WhenNameUnchanged_Succeeds()
    {
        // Arrange
        var course = Course.Create(_organizationId, "Original Name");
        _dbContext.Courses.Add(course);
        await _dbContext.SaveChangesAsync();

        var dto = new UpdateCourseDto
        {
            Name = "Original Name",
            Description = "Updated Description"
        };

        // Act
        var act = () => _validator.ValidateUpdateAsync(course.Id, dto, _organizationId, CancellationToken.None);

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ValidateUpdateAsync_WhenNameCollidesWithAnotherCourseInSameOrg_ThrowsConflictException()
    {
        // Arrange
        var course1 = Course.Create(_organizationId, "Course One");
        var course2 = Course.Create(_organizationId, "Course Two");
        _dbContext.Courses.AddRange(course1, course2);
        await _dbContext.SaveChangesAsync();

        var dto = new UpdateCourseDto
        {
            Name = "Course Two"
        };

        // Act
        var act = () => _validator.ValidateUpdateAsync(course1.Id, dto, _organizationId, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ConflictException>()
            .WithMessage("*already exists in this organization*");
    }

    #endregion

    #region ValidateDeleteAsync

    [Fact]
    public async Task ValidateDeleteAsync_WhenCourseNotFound_ThrowsNotFoundException()
    {
        // Act
        var act = () => _validator.ValidateDeleteAsync(Guid.NewGuid(), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("*Course*");
    }

    [Fact]
    public async Task ValidateDeleteAsync_WhenCourseHasCourseGroups_ThrowsConflictException()
    {
        // Arrange
        var course = Course.Create(_organizationId, "Course With Groups");
        _dbContext.Courses.Add(course);

        var group = new CourseGroup
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            OrganizationId = _organizationId,
            Name = "Group Alpha",
            StartDate = DateOnly.FromDateTime(DateTime.UtcNow),
            EndDate = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(1))
        };
        _dbContext.CourseGroups.Add(group);
        await _dbContext.SaveChangesAsync();

        // Act
        var act = () => _validator.ValidateDeleteAsync(course.Id, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ConflictException>()
            .WithMessage("*associated course group*");
    }

    [Fact]
    public async Task ValidateDeleteAsync_WhenCourseHasNoDependencies_Succeeds()
    {
        // Arrange
        var course = Course.Create(_organizationId, "Isolated Course");
        _dbContext.Courses.Add(course);
        await _dbContext.SaveChangesAsync();

        // Act
        var act = () => _validator.ValidateDeleteAsync(course.Id, CancellationToken.None);

        // Assert
        await act.Should().NotThrowAsync();
    }

    #endregion

    #region ValidateQuery

    [Fact]
    public void ValidateQuery_WithValidParameters_DoesNotThrow()
    {
        // Arrange
        var query = new CourseQueryParameters
        {
            PageNumber = 1,
            PageSize = 20,
            SearchTerm = "Architect",
            Category = "Programming",
            MinPrice = 10,
            MaxPrice = 500,
            MinDuration = 5,
            MaxDuration = 50,
            SortBy = "BasePrice",
            Status = CourseConstants.Statuses.Active
        };

        // Act
        var act = () => _validator.ValidateQuery(query);

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void ValidateQuery_WithNull_ThrowsArgumentNullException()
    {
        // Act
        var act = () => _validator.ValidateQuery(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void ValidateQuery_WithInvalidPageNumber_ThrowsValidationException(int page)
    {
        // Arrange
        var query = new CourseQueryParameters { PageNumber = page };
        // Force the backing field bypass if necessary, or pass invalid value
        typeof(CourseQueryParameters).GetField("_pageNumber", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .SetValue(query, page);

        // Act
        var act = () => _validator.ValidateQuery(query);

        // Assert
        act.Should().Throw<ValidationException>();
    }

    [Fact]
    public void ValidateQuery_WithMinPriceGreaterThanMaxPrice_ThrowsValidationException()
    {
        // Arrange
        var query = new CourseQueryParameters
        {
            MinPrice = 500,
            MaxPrice = 100
        };

        // Act
        var act = () => _validator.ValidateQuery(query);

        // Assert
        act.Should().Throw<ValidationException>()
            .Where(ex => ex.Errors.ContainsKey(nameof(query.MinPrice)));
    }

    [Fact]
    public void ValidateQuery_WithMinDurationGreaterThanMaxDuration_ThrowsValidationException()
    {
        // Arrange
        var query = new CourseQueryParameters
        {
            MinDuration = 100,
            MaxDuration = 20
        };

        // Act
        var act = () => _validator.ValidateQuery(query);

        // Assert
        act.Should().Throw<ValidationException>()
            .Where(ex => ex.Errors.ContainsKey(nameof(query.MinDuration)));
    }

    [Fact]
    public void ValidateQuery_WithInvalidStatus_ThrowsValidationException()
    {
        // Arrange
        var query = new CourseQueryParameters
        {
            Status = 99
        };

        // Act
        var act = () => _validator.ValidateQuery(query);

        // Assert
        act.Should().Throw<ValidationException>()
            .Where(ex => ex.Errors.ContainsKey(nameof(query.Status)));
    }

    [Fact]
    public void ValidateQuery_WithInvalidSortField_ThrowsValidationException()
    {
        // Arrange
        var query = new CourseQueryParameters
        {
            SortBy = "NonExistentField"
        };

        // Act
        var act = () => _validator.ValidateQuery(query);

        // Assert
        act.Should().Throw<ValidationException>()
            .Where(ex => ex.Errors.ContainsKey(nameof(query.SortBy)));
    }

    #endregion
}
