using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using TrainHub.Application.Common.Exceptions;
using TrainHub.Application.Features.CourseGroups.DTOs;
using TrainHub.Application.Services;
using TrainHub.Domain.Constants;
using TrainHub.Domain.Models;
using TrainHub.Infrastructure.Data;
using TrainHub.UnitTests.Common;

namespace TrainHub.UnitTests.Services;

public class CourseGroupValidatorTests : IDisposable
{
    private readonly TrainHubDbContext _dbContext;
    private readonly Mock<ILogger<CourseGroupValidator>> _loggerMock;
    private readonly CourseGroupValidator _validator;
    private readonly Guid _organizationId = Guid.NewGuid();
    private readonly Guid _otherOrgId = Guid.NewGuid();
    private readonly Course _course;
    private readonly Instructor _instructor;

    public CourseGroupValidatorTests()
    {
        _dbContext = TestHelpers.CreateInMemoryDbContext();
        _loggerMock = new Mock<ILogger<CourseGroupValidator>>();
        _validator = new CourseGroupValidator(_dbContext, _loggerMock.Object);

        // Seed test organization
        var org = new Organization
        {
            Id = _organizationId,
            Name = "Test Training Center",
            Slug = "test-tc",
            IsActive = true
        };
        var otherOrg = new Organization
        {
            Id = _otherOrgId,
            Name = "Other Training Center",
            Slug = "other-tc",
            IsActive = true
        };
        _dbContext.Organizations.AddRange(org, otherOrg);

        // Seed course in main org
        _course = Course.Create(_organizationId, "C# Web API");
        _dbContext.Courses.Add(_course);

        // Seed instructor in main org
        _instructor = Instructor.Create(Guid.NewGuid(), _organizationId, "Backend Engineering");
        _dbContext.Instructors.Add(_instructor);

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
        var dto = new CreateCourseGroupDto
        {
            CourseId = _course.Id,
            InstructorId = _instructor.Id,
            Name = "Batch 1",
            StartDate = new DateOnly(2026, 10, 1),
            EndDate = new DateOnly(2026, 12, 1),
            ScheduleDescription = "Mon & Wed",
            Capacity = 30
        };

        // Act
        var act = () => _validator.ValidateCreateAsync(dto, _organizationId, CancellationToken.None);

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ValidateCreateAsync_WithNullDto_ThrowsArgumentNullException()
    {
        // Act
        var act = () => _validator.ValidateCreateAsync(null!, _organizationId, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task ValidateCreateAsync_WithEndDateBeforeStartDate_ThrowsValidationException()
    {
        // Arrange
        var dto = new CreateCourseGroupDto
        {
            CourseId = _course.Id,
            Name = "Batch 1",
            StartDate = new DateOnly(2026, 12, 1),
            EndDate = new DateOnly(2026, 10, 1)
        };

        // Act
        var act = () => _validator.ValidateCreateAsync(dto, _organizationId, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ValidationException>()
            .Where(ex => ex.Errors.ContainsKey(nameof(dto.EndDate)));
    }

    [Fact]
    public async Task ValidateCreateAsync_WithInvalidCapacity_ThrowsValidationException()
    {
        // Arrange
        var dto = new CreateCourseGroupDto
        {
            CourseId = _course.Id,
            Name = "Batch 1",
            StartDate = new DateOnly(2026, 10, 1),
            EndDate = new DateOnly(2026, 12, 1),
            Capacity = 5000
        };

        // Act
        var act = () => _validator.ValidateCreateAsync(dto, _organizationId, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ValidationException>()
            .Where(ex => ex.Errors.ContainsKey(nameof(dto.Capacity)));
    }

    [Fact]
    public async Task ValidateCreateAsync_WithNonExistentOrg_ThrowsNotFoundException()
    {
        // Arrange
        var dto = new CreateCourseGroupDto
        {
            CourseId = _course.Id,
            Name = "Batch 1",
            StartDate = new DateOnly(2026, 10, 1),
            EndDate = new DateOnly(2026, 12, 1)
        };

        // Act
        var act = () => _validator.ValidateCreateAsync(dto, Guid.NewGuid(), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("*Organization*");
    }

    [Fact]
    public async Task ValidateCreateAsync_WithNonExistentCourse_ThrowsNotFoundException()
    {
        // Arrange
        var dto = new CreateCourseGroupDto
        {
            CourseId = Guid.NewGuid(),
            Name = "Batch 1",
            StartDate = new DateOnly(2026, 10, 1),
            EndDate = new DateOnly(2026, 12, 1)
        };

        // Act
        var act = () => _validator.ValidateCreateAsync(dto, _organizationId, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("*Course*");
    }

    [Fact]
    public async Task ValidateCreateAsync_WithCourseInDifferentOrg_ThrowsNotFoundException()
    {
        // Arrange - course belonging to otherOrg
        var otherCourse = Course.Create(_otherOrgId, "Other Course");
        _dbContext.Courses.Add(otherCourse);
        await _dbContext.SaveChangesAsync();

        var dto = new CreateCourseGroupDto
        {
            CourseId = otherCourse.Id,
            Name = "Batch 1",
            StartDate = new DateOnly(2026, 10, 1),
            EndDate = new DateOnly(2026, 12, 1)
        };

        // Act
        var act = () => _validator.ValidateCreateAsync(dto, _organizationId, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("*Course*");
    }

    [Fact]
    public async Task ValidateCreateAsync_WithNonExistentInstructor_ThrowsNotFoundException()
    {
        // Arrange
        var dto = new CreateCourseGroupDto
        {
            CourseId = _course.Id,
            InstructorId = Guid.NewGuid(),
            Name = "Batch 1",
            StartDate = new DateOnly(2026, 10, 1),
            EndDate = new DateOnly(2026, 12, 1)
        };

        // Act
        var act = () => _validator.ValidateCreateAsync(dto, _organizationId, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("*Instructor*");
    }

    [Fact]
    public async Task ValidateCreateAsync_WithInstructorInDifferentOrg_ThrowsNotFoundException()
    {
        // Arrange - instructor belonging to otherOrg
        var otherInstructor = Instructor.Create(Guid.NewGuid(), _otherOrgId, "Frontend");
        _dbContext.Instructors.Add(otherInstructor);
        await _dbContext.SaveChangesAsync();

        var dto = new CreateCourseGroupDto
        {
            CourseId = _course.Id,
            InstructorId = otherInstructor.Id,
            Name = "Batch 1",
            StartDate = new DateOnly(2026, 10, 1),
            EndDate = new DateOnly(2026, 12, 1)
        };

        // Act
        var act = () => _validator.ValidateCreateAsync(dto, _organizationId, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("*Instructor*");
    }

    [Fact]
    public async Task ValidateCreateAsync_WithDuplicateNameInSameCourse_ThrowsConflictException()
    {
        // Arrange
        var existingGroup = CourseGroup.Create(
            _organizationId,
            _course.Id,
            "Cohort A",
            new DateOnly(2026, 10, 1),
            new DateOnly(2026, 12, 1));
        _dbContext.CourseGroups.Add(existingGroup);
        await _dbContext.SaveChangesAsync();

        var dto = new CreateCourseGroupDto
        {
            CourseId = _course.Id,
            Name = "  Cohort A  ",
            StartDate = new DateOnly(2026, 11, 1),
            EndDate = new DateOnly(2026, 12, 1)
        };

        // Act
        var act = () => _validator.ValidateCreateAsync(dto, _organizationId, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ConflictException>()
            .WithMessage("*already exists for this course*");
    }

    [Fact]
    public async Task ValidateCreateAsync_WithSameNameInDifferentCourse_Succeeds()
    {
        // Arrange
        var course2 = Course.Create(_organizationId, "C# Basics");
        _dbContext.Courses.Add(course2);

        var existingGroup = CourseGroup.Create(
            _organizationId,
            _course.Id,
            "Cohort A",
            new DateOnly(2026, 10, 1),
            new DateOnly(2026, 12, 1));
        _dbContext.CourseGroups.Add(existingGroup);
        await _dbContext.SaveChangesAsync();

        var dto = new CreateCourseGroupDto
        {
            CourseId = course2.Id,
            Name = "Cohort A",
            StartDate = new DateOnly(2026, 11, 1),
            EndDate = new DateOnly(2026, 12, 1)
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
        var group = CourseGroup.Create(
            _organizationId,
            _course.Id,
            "Group A",
            new DateOnly(2026, 10, 1),
            new DateOnly(2026, 12, 1));
        _dbContext.CourseGroups.Add(group);
        await _dbContext.SaveChangesAsync();

        var dto = new UpdateCourseGroupDto
        {
            Name = "Group A Updated",
            StartDate = new DateOnly(2026, 10, 5),
            EndDate = new DateOnly(2026, 12, 15),
            InstructorId = _instructor.Id,
            Capacity = 35
        };

        // Act
        var act = () => _validator.ValidateUpdateAsync(group.Id, dto, _organizationId, CancellationToken.None);

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ValidateUpdateAsync_WithNonExistentGroup_ThrowsNotFoundException()
    {
        // Arrange
        var dto = new UpdateCourseGroupDto
        {
            Name = "Group A",
            StartDate = new DateOnly(2026, 10, 1),
            EndDate = new DateOnly(2026, 12, 1)
        };

        // Act
        var act = () => _validator.ValidateUpdateAsync(Guid.NewGuid(), dto, _organizationId, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("*CourseGroup*");
    }

    [Fact]
    public async Task ValidateUpdateAsync_WithDuplicateNameInSameCourse_ThrowsConflictException()
    {
        // Arrange
        var group1 = CourseGroup.Create(_organizationId, _course.Id, "Group 1", new DateOnly(2026, 10, 1), new DateOnly(2026, 12, 1));
        var group2 = CourseGroup.Create(_organizationId, _course.Id, "Group 2", new DateOnly(2026, 10, 1), new DateOnly(2026, 12, 1));
        _dbContext.CourseGroups.AddRange(group1, group2);
        await _dbContext.SaveChangesAsync();

        var dto = new UpdateCourseGroupDto
        {
            Name = "Group 1",
            StartDate = new DateOnly(2026, 10, 1),
            EndDate = new DateOnly(2026, 12, 1)
        };

        // Act - Updating group2 to have group1's name
        var act = () => _validator.ValidateUpdateAsync(group2.Id, dto, _organizationId, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ConflictException>()
            .WithMessage("*already exists for this course*");
    }

    [Fact]
    public async Task ValidateUpdateAsync_WithSameNameForSameGroup_Succeeds()
    {
        // Arrange
        var group = CourseGroup.Create(_organizationId, _course.Id, "Group 1", new DateOnly(2026, 10, 1), new DateOnly(2026, 12, 1));
        _dbContext.CourseGroups.Add(group);
        await _dbContext.SaveChangesAsync();

        var dto = new UpdateCourseGroupDto
        {
            Name = "Group 1",
            StartDate = new DateOnly(2026, 10, 5),
            EndDate = new DateOnly(2026, 12, 5)
        };

        // Act
        var act = () => _validator.ValidateUpdateAsync(group.Id, dto, _organizationId, CancellationToken.None);

        // Assert
        await act.Should().NotThrowAsync();
    }

    #endregion

    #region ValidateDeleteAsync

    [Fact]
    public async Task ValidateDeleteAsync_WithNoDependencies_Succeeds()
    {
        // Arrange
        var group = CourseGroup.Create(_organizationId, _course.Id, "Group to Delete", new DateOnly(2026, 10, 1), new DateOnly(2026, 12, 1));
        _dbContext.CourseGroups.Add(group);
        await _dbContext.SaveChangesAsync();

        // Act
        var act = () => _validator.ValidateDeleteAsync(group.Id, CancellationToken.None);

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ValidateDeleteAsync_WithNonExistentGroup_ThrowsNotFoundException()
    {
        // Act
        var act = () => _validator.ValidateDeleteAsync(Guid.NewGuid(), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("*CourseGroup*");
    }

    [Fact]
    public async Task ValidateDeleteAsync_WithEnrollments_ThrowsConflictException()
    {
        // Arrange
        var group = CourseGroup.Create(_organizationId, _course.Id, "Group with Enrollments", new DateOnly(2026, 10, 1), new DateOnly(2026, 12, 1));
        _dbContext.CourseGroups.Add(group);

        var enrollment = Enrollment.Create(_organizationId, group.Id, Guid.NewGuid(), paidAmount: 100m);
        _dbContext.Enrollments.Add(enrollment);
        await _dbContext.SaveChangesAsync();

        // Act
        var act = () => _validator.ValidateDeleteAsync(group.Id, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ConflictException>()
            .WithMessage("*associated records*enrollment*");
    }

    [Fact]
    public async Task ValidateDeleteAsync_WithSessions_ThrowsConflictException()
    {
        // Arrange
        var group = CourseGroup.Create(_organizationId, _course.Id, "Group with Sessions", new DateOnly(2026, 10, 1), new DateOnly(2026, 12, 1));
        _dbContext.CourseGroups.Add(group);

        var session = new Session
        {
            Id = Guid.NewGuid(),
            OrganizationId = _organizationId,
            CourseGroupId = group.Id,
            StartTime = DateTime.UtcNow.AddDays(1),
            EndTime = DateTime.UtcNow.AddDays(1).AddHours(2)
        };
        _dbContext.Sessions.Add(session);
        await _dbContext.SaveChangesAsync();

        // Act
        var act = () => _validator.ValidateDeleteAsync(group.Id, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ConflictException>()
            .WithMessage("*associated records*session*");
    }

    #endregion

    #region ValidateQuery

    [Theory]
    [InlineData(0, 10)]
    [InlineData(-1, 10)]
    [InlineData(1, 0)]
    [InlineData(1, -5)]
    [InlineData(1, 101)]
    public void ValidateQuery_WithInvalidPaging_ThrowsValidationException(int pageNumber, int pageSize)
    {
        // Arrange
        var query = new CourseGroupQueryParameters();
        typeof(CourseGroupQueryParameters).GetField("_pageNumber", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .SetValue(query, pageNumber);
        typeof(CourseGroupQueryParameters).GetField("_pageSize", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .SetValue(query, pageSize);

        // Act
        var act = () => _validator.ValidateQuery(query);

        // Assert
        act.Should().Throw<ValidationException>();
    }

    [Fact]
    public void ValidateQuery_WithInvalidSortField_ThrowsValidationException()
    {
        // Arrange
        var query = new CourseGroupQueryParameters
        {
            SortBy = "NonExistentField"
        };

        // Act
        var act = () => _validator.ValidateQuery(query);

        // Assert
        act.Should().Throw<ValidationException>()
            .Where(ex => ex.Errors.ContainsKey(nameof(query.SortBy)));
    }

    [Fact]
    public void ValidateQuery_WithValidParameters_Succeeds()
    {
        // Arrange
        var query = new CourseGroupQueryParameters
        {
            PageNumber = 1,
            PageSize = 20,
            SearchTerm = "Batch",
            SortBy = "StartDate",
            SortDescending = true
        };

        // Act
        var act = () => _validator.ValidateQuery(query);

        // Assert
        act.Should().NotThrow();
    }

    #endregion

    #region ValidateAssignInstructorAsync

    [Fact]
    public async Task ValidateAssignInstructorAsync_WithValidInstructor_Succeeds()
    {
        // Arrange
        var group = CourseGroup.Create(_organizationId, _course.Id, "Group 1", new DateOnly(2026, 10, 1), new DateOnly(2026, 12, 1));
        _dbContext.CourseGroups.Add(group);
        await _dbContext.SaveChangesAsync();

        // Act
        var act = () => _validator.ValidateAssignInstructorAsync(group.Id, _instructor.Id, _organizationId, CancellationToken.None);

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ValidateAssignInstructorAsync_WithNullInstructorId_Succeeds()
    {
        // Arrange
        var group = CourseGroup.Create(_organizationId, _course.Id, "Group 1", new DateOnly(2026, 10, 1), new DateOnly(2026, 12, 1), instructorId: _instructor.Id);
        _dbContext.CourseGroups.Add(group);
        await _dbContext.SaveChangesAsync();

        // Act
        var act = () => _validator.ValidateAssignInstructorAsync(group.Id, null, _organizationId, CancellationToken.None);

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ValidateAssignInstructorAsync_WithNonExistentGroup_ThrowsNotFoundException()
    {
        // Act
        var act = () => _validator.ValidateAssignInstructorAsync(Guid.NewGuid(), _instructor.Id, _organizationId, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("*CourseGroup*");
    }

    [Fact]
    public async Task ValidateAssignInstructorAsync_WithCancelledGroup_ThrowsConflictException()
    {
        // Arrange
        var group = CourseGroup.Create(
            _organizationId, _course.Id, "Group Cancelled", new DateOnly(2026, 10, 1), new DateOnly(2026, 12, 1),
            status: CourseGroupConstants.Statuses.Cancelled);
        _dbContext.CourseGroups.Add(group);
        await _dbContext.SaveChangesAsync();

        // Act
        var act = () => _validator.ValidateAssignInstructorAsync(group.Id, _instructor.Id, _organizationId, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ConflictException>()
            .WithMessage("*cancelled*");
    }

    [Fact]
    public async Task ValidateAssignInstructorAsync_WithCompletedGroup_ThrowsConflictException()
    {
        // Arrange
        var group = CourseGroup.Create(
            _organizationId, _course.Id, "Group Completed", new DateOnly(2026, 10, 1), new DateOnly(2026, 12, 1),
            status: CourseGroupConstants.Statuses.Completed);
        _dbContext.CourseGroups.Add(group);
        await _dbContext.SaveChangesAsync();

        // Act
        var act = () => _validator.ValidateAssignInstructorAsync(group.Id, _instructor.Id, _organizationId, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ConflictException>()
            .WithMessage("*completed*");
    }

    [Fact]
    public async Task ValidateAssignInstructorAsync_WithNonExistentInstructor_ThrowsNotFoundException()
    {
        // Arrange
        var group = CourseGroup.Create(_organizationId, _course.Id, "Group 1", new DateOnly(2026, 10, 1), new DateOnly(2026, 12, 1));
        _dbContext.CourseGroups.Add(group);
        await _dbContext.SaveChangesAsync();

        // Act
        var act = () => _validator.ValidateAssignInstructorAsync(group.Id, Guid.NewGuid(), _organizationId, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("*Instructor*");
    }

    [Fact]
    public async Task ValidateAssignInstructorAsync_WithInstructorFromDifferentOrg_ThrowsNotFoundException()
    {
        // Arrange
        var otherInstructor = Instructor.Create(Guid.NewGuid(), _otherOrgId, "DevOps");
        _dbContext.Instructors.Add(otherInstructor);

        var group = CourseGroup.Create(_organizationId, _course.Id, "Group 1", new DateOnly(2026, 10, 1), new DateOnly(2026, 12, 1));
        _dbContext.CourseGroups.Add(group);
        await _dbContext.SaveChangesAsync();

        // Act
        var act = () => _validator.ValidateAssignInstructorAsync(group.Id, otherInstructor.Id, _organizationId, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("*Instructor*");
    }

    [Fact]
    public async Task ValidateAssignInstructorAsync_WithInactiveInstructor_ThrowsConflictException()
    {
        // Arrange
        var inactiveUser = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            OrganizationId = _organizationId,
            FirstName = "Inactive",
            LastName = "Teacher",
            IsActive = false
        };
        _dbContext.ApplicationUsers.Add(inactiveUser);

        var inactiveInstructor = Instructor.Create(inactiveUser.Id, _organizationId, "Math");
        _dbContext.Instructors.Add(inactiveInstructor);

        var group = CourseGroup.Create(_organizationId, _course.Id, "Group 1", new DateOnly(2026, 10, 1), new DateOnly(2026, 12, 1));
        _dbContext.CourseGroups.Add(group);
        await _dbContext.SaveChangesAsync();

        // Act
        var act = () => _validator.ValidateAssignInstructorAsync(group.Id, inactiveInstructor.Id, _organizationId, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ConflictException>()
            .WithMessage("*inactive*");
    }

    #endregion

    #region ValidateUpdateCapacityAsync

    [Fact]
    public async Task ValidateUpdateCapacityAsync_WithValidCapacity_Succeeds()
    {
        // Arrange
        var group = CourseGroup.Create(_organizationId, _course.Id, "Group 1", new DateOnly(2026, 10, 1), new DateOnly(2026, 12, 1), capacity: 20);
        _dbContext.CourseGroups.Add(group);
        await _dbContext.SaveChangesAsync();

        // Act
        var act = () => _validator.ValidateUpdateCapacityAsync(group.Id, 40, CancellationToken.None);

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(1001)]
    public async Task ValidateUpdateCapacityAsync_WithInvalidCapacity_ThrowsValidationException(int invalidCapacity)
    {
        // Act
        var act = () => _validator.ValidateUpdateCapacityAsync(Guid.NewGuid(), invalidCapacity, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ValidationException>()
            .Where(ex => ex.Errors.ContainsKey("capacity"));
    }

    [Fact]
    public async Task ValidateUpdateCapacityAsync_WithNonExistentGroup_ThrowsNotFoundException()
    {
        // Act
        var act = () => _validator.ValidateUpdateCapacityAsync(Guid.NewGuid(), 30, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("*CourseGroup*");
    }

    [Fact]
    public async Task ValidateUpdateCapacityAsync_WithCancelledGroup_ThrowsConflictException()
    {
        // Arrange
        var group = CourseGroup.Create(
            _organizationId, _course.Id, "Group 1", new DateOnly(2026, 10, 1), new DateOnly(2026, 12, 1),
            status: CourseGroupConstants.Statuses.Cancelled);
        _dbContext.CourseGroups.Add(group);
        await _dbContext.SaveChangesAsync();

        // Act
        var act = () => _validator.ValidateUpdateCapacityAsync(group.Id, 30, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ConflictException>()
            .WithMessage("*cancelled*");
    }

    [Fact]
    public async Task ValidateUpdateCapacityAsync_WithCompletedGroup_ThrowsConflictException()
    {
        // Arrange
        var group = CourseGroup.Create(
            _organizationId, _course.Id, "Group 1", new DateOnly(2026, 10, 1), new DateOnly(2026, 12, 1),
            status: CourseGroupConstants.Statuses.Completed);
        _dbContext.CourseGroups.Add(group);
        await _dbContext.SaveChangesAsync();

        // Act
        var act = () => _validator.ValidateUpdateCapacityAsync(group.Id, 30, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ConflictException>()
            .WithMessage("*completed*");
    }

    [Fact]
    public async Task ValidateUpdateCapacityAsync_WithCapacityBelowEnrolledCount_ThrowsConflictException()
    {
        // Arrange
        var group = CourseGroup.Create(_organizationId, _course.Id, "Group 1", new DateOnly(2026, 10, 1), new DateOnly(2026, 12, 1), capacity: 30);
        _dbContext.CourseGroups.Add(group);

        // Add 10 enrollments
        for (int i = 0; i < 10; i++)
        {
            _dbContext.Enrollments.Add(Enrollment.Create(_organizationId, group.Id, Guid.NewGuid()));
        }
        await _dbContext.SaveChangesAsync();

        // Act - Attempt to set capacity to 5 (< 10)
        var act = () => _validator.ValidateUpdateCapacityAsync(group.Id, 5, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ConflictException>()
            .WithMessage("*Capacity cannot be reduced to 5 because 10 student(s) are already enrolled*");
    }

    #endregion

    #region ValidateUpdateStatusAsync

    [Fact]
    public async Task ValidateUpdateStatusAsync_WithValidStatus_Succeeds()
    {
        // Arrange
        var group = CourseGroup.Create(_organizationId, _course.Id, "Group 1", new DateOnly(2026, 10, 1), new DateOnly(2026, 12, 1));
        _dbContext.CourseGroups.Add(group);
        await _dbContext.SaveChangesAsync();

        // Act
        var act = () => _validator.ValidateUpdateStatusAsync(group.Id, CourseGroupConstants.Statuses.Completed, CancellationToken.None);

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ValidateUpdateStatusAsync_WithInvalidStatus_ThrowsValidationException()
    {
        // Act
        var act = () => _validator.ValidateUpdateStatusAsync(Guid.NewGuid(), 99, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ValidationException>()
            .Where(ex => ex.Errors.ContainsKey("status"));
    }

    [Fact]
    public async Task ValidateUpdateStatusAsync_WithNonExistentGroup_ThrowsNotFoundException()
    {
        // Act
        var act = () => _validator.ValidateUpdateStatusAsync(Guid.NewGuid(), CourseGroupConstants.Statuses.Active, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("*CourseGroup*");
    }

    [Fact]
    public async Task ValidateUpdateStatusAsync_WhenModifyingCancelledGroup_ThrowsConflictException()
    {
        // Arrange
        var group = CourseGroup.Create(
            _organizationId, _course.Id, "Group 1", new DateOnly(2026, 10, 1), new DateOnly(2026, 12, 1),
            status: CourseGroupConstants.Statuses.Cancelled);
        _dbContext.CourseGroups.Add(group);
        await _dbContext.SaveChangesAsync();

        // Act
        var act = () => _validator.ValidateUpdateStatusAsync(group.Id, CourseGroupConstants.Statuses.Active, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ConflictException>()
            .WithMessage("*cancelled*");
    }

    [Fact]
    public async Task ValidateUpdateStatusAsync_WhenRevertingCompletedGroupToDraft_ThrowsConflictException()
    {
        // Arrange
        var group = CourseGroup.Create(
            _organizationId, _course.Id, "Group 1", new DateOnly(2026, 10, 1), new DateOnly(2026, 12, 1),
            status: CourseGroupConstants.Statuses.Completed);
        _dbContext.CourseGroups.Add(group);
        await _dbContext.SaveChangesAsync();

        // Act
        var act = () => _validator.ValidateUpdateStatusAsync(group.Id, CourseGroupConstants.Statuses.Draft, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ConflictException>()
            .WithMessage("*draft*");
    }

    [Fact]
    public async Task ValidateUpdateStatusAsync_WhenActivatingGroupWithArchivedCourse_ThrowsConflictException()
    {
        // Arrange
        var archivedCourse = Course.Create(_organizationId, "Archived Course", status: CourseConstants.Statuses.Archived);
        _dbContext.Courses.Add(archivedCourse);

        var group = CourseGroup.Create(
            _organizationId, archivedCourse.Id, "Group 1", new DateOnly(2026, 10, 1), new DateOnly(2026, 12, 1),
            status: CourseGroupConstants.Statuses.Draft);
        _dbContext.CourseGroups.Add(group);
        await _dbContext.SaveChangesAsync();

        // Act
        var act = () => _validator.ValidateUpdateStatusAsync(group.Id, CourseGroupConstants.Statuses.Active, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ConflictException>()
            .WithMessage("*archived*");
    }

    #endregion

    #region Additional Create & Update Business Rules

    [Fact]
    public async Task ValidateCreateAsync_WithArchivedCourse_ThrowsConflictException()
    {
        // Arrange
        var archivedCourse = Course.Create(_organizationId, "Archived Course", status: CourseConstants.Statuses.Archived);
        _dbContext.Courses.Add(archivedCourse);
        await _dbContext.SaveChangesAsync();

        var dto = new CreateCourseGroupDto
        {
            CourseId = archivedCourse.Id,
            Name = "Group Alpha",
            StartDate = new DateOnly(2026, 10, 1),
            EndDate = new DateOnly(2026, 12, 1)
        };

        // Act
        var act = () => _validator.ValidateCreateAsync(dto, _organizationId, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ConflictException>()
            .WithMessage("*archived*");
    }

    [Fact]
    public async Task ValidateUpdateAsync_WhenGroupIsCancelled_ThrowsConflictException()
    {
        // Arrange
        var group = CourseGroup.Create(
            _organizationId, _course.Id, "Group 1", new DateOnly(2026, 10, 1), new DateOnly(2026, 12, 1),
            status: CourseGroupConstants.Statuses.Cancelled);
        _dbContext.CourseGroups.Add(group);
        await _dbContext.SaveChangesAsync();

        var dto = new UpdateCourseGroupDto
        {
            Name = "New Name",
            StartDate = new DateOnly(2026, 10, 1),
            EndDate = new DateOnly(2026, 12, 1)
        };

        // Act
        var act = () => _validator.ValidateUpdateAsync(group.Id, dto, _organizationId, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ConflictException>()
            .WithMessage("*cancelled*");
    }

    [Fact]
    public async Task ValidateUpdateAsync_WhenReducingCapacityBelowEnrolledCount_ThrowsConflictException()
    {
        // Arrange
        var group = CourseGroup.Create(_organizationId, _course.Id, "Group 1", new DateOnly(2026, 10, 1), new DateOnly(2026, 12, 1), capacity: 30);
        _dbContext.CourseGroups.Add(group);

        for (int i = 0; i < 8; i++)
        {
            _dbContext.Enrollments.Add(Enrollment.Create(_organizationId, group.Id, Guid.NewGuid()));
        }
        await _dbContext.SaveChangesAsync();

        var dto = new UpdateCourseGroupDto
        {
            Name = "Group 1",
            StartDate = new DateOnly(2026, 10, 1),
            EndDate = new DateOnly(2026, 12, 1),
            Capacity = 5
        };

        // Act
        var act = () => _validator.ValidateUpdateAsync(group.Id, dto, _organizationId, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ConflictException>()
            .WithMessage("*Capacity cannot be reduced to 5 because 8 student(s) are already enrolled*");
    }

    #endregion
}
