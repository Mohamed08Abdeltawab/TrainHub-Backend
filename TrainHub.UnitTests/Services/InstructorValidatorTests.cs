using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using TrainHub.Application.Common.Exceptions;
using TrainHub.Application.Features.Instructors.DTOs;
using TrainHub.Domain.Constants;
using TrainHub.Domain.Models;
using TrainHub.Infrastructure.Data;
using TrainHub.Application.Services;
using TrainHub.UnitTests.Common;

namespace TrainHub.UnitTests.Services;

public class InstructorValidatorTests : IDisposable
{
    private readonly TrainHubDbContext _dbContext;
    private readonly Mock<ILogger<InstructorValidator>> _loggerMock;
    private readonly InstructorValidator _validator;
    private readonly Guid _organizationId = Guid.NewGuid();

    public InstructorValidatorTests()
    {
        _dbContext = TestHelpers.CreateInMemoryDbContext();
        _loggerMock = new Mock<ILogger<InstructorValidator>>();
        _validator = new InstructorValidator(_dbContext, _loggerMock.Object);
    }

    public void Dispose()
    {
        _dbContext.Database.EnsureDeleted();
        _dbContext.Dispose();
    }

    [Fact]
    public async Task ValidateCreateAsync_WhenUserNotFound_ThrowsNotFoundException()
    {
        // Arrange
        var dto = new CreateInstructorDto
        {
            UserId = Guid.NewGuid(),
            Specialization = "Software Engineering"
        };

        // Act
        var act = () => _validator.ValidateCreateAsync(dto, _organizationId, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("*User*");
    }

    [Fact]
    public async Task ValidateCreateAsync_WhenUserInDifferentOrg_ThrowsBadRequestException()
    {
        // Arrange
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            OrganizationId = Guid.NewGuid(), // different org
            FirstName = "Test",
            LastName = "User",
            Email = "instructor@example.com"
        };
        _dbContext.ApplicationUsers.Add(user);
        await _dbContext.SaveChangesAsync();

        var dto = new CreateInstructorDto
        {
            UserId = user.Id,
            Specialization = "Software Engineering"
        };

        // Act
        var act = () => _validator.ValidateCreateAsync(dto, _organizationId, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<BadRequestException>()
            .WithMessage("*User does not belong to the target organization*");
    }

    [Fact]
    public async Task ValidateCreateAsync_WhenInstructorAlreadyExistsForUser_ThrowsConflictException()
    {
        // Arrange
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            OrganizationId = _organizationId,
            FirstName = "Existing",
            LastName = "Instructor",
            Email = "existing@example.com"
        };
        _dbContext.ApplicationUsers.Add(user);

        var existingInstructor = Instructor.Create(user.Id, _organizationId, "Existing Spec");
        _dbContext.Instructors.Add(existingInstructor);
        await _dbContext.SaveChangesAsync();

        var dto = new CreateInstructorDto
        {
            UserId = user.Id,
            Specialization = "New Spec"
        };

        // Act
        var act = () => _validator.ValidateCreateAsync(dto, _organizationId, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ConflictException>()
            .WithMessage("*already registered as an instructor*");
    }

    [Fact]
    public async Task ValidateCreateAsync_WhenValid_DoesNotThrow()
    {
        // Arrange
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            OrganizationId = _organizationId,
            FirstName = "Valid",
            LastName = "User",
            Email = "valid@example.com"
        };
        _dbContext.ApplicationUsers.Add(user);
        await _dbContext.SaveChangesAsync();

        var dto = new CreateInstructorDto
        {
            UserId = user.Id,
            Specialization = "Backend Development",
            Bio = "Experienced instructor"
        };

        // Act
        var act = () => _validator.ValidateCreateAsync(dto, _organizationId, CancellationToken.None);

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ValidateUpdateAsync_WhenInstructorNotFound_ThrowsNotFoundException()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();
        var dto = new UpdateInstructorDto
        {
            Specialization = "Updated Spec"
        };

        // Act
        var act = () => _validator.ValidateUpdateAsync(nonExistentId, dto, _organizationId, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("*Instructor*");
    }

    [Fact]
    public async Task ValidateUpdateAsync_WhenValid_DoesNotThrow()
    {
        // Arrange
        var instructor = Instructor.Create(Guid.NewGuid(), _organizationId, "Initial Spec");
        _dbContext.Instructors.Add(instructor);
        await _dbContext.SaveChangesAsync();

        var dto = new UpdateInstructorDto
        {
            Specialization = "Updated Spec",
            Bio = "Updated bio"
        };

        // Act
        var act = () => _validator.ValidateUpdateAsync(instructor.Id, dto, _organizationId, CancellationToken.None);

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ValidateUpdateAsync_WhenInstructorInDifferentOrg_ThrowsBadRequestException()
    {
        // Arrange
        var instructor = Instructor.Create(Guid.NewGuid(), Guid.NewGuid(), "Initial Spec"); // different org
        _dbContext.Instructors.Add(instructor);
        await _dbContext.SaveChangesAsync();

        var dto = new UpdateInstructorDto
        {
            Specialization = "Updated Spec"
        };

        // Act
        var act = () => _validator.ValidateUpdateAsync(instructor.Id, dto, _organizationId, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<BadRequestException>()
            .WithMessage("*Instructor does not belong to the target organization*");
    }

    [Fact]
    public async Task ValidateDeleteAsync_WhenInstructorNotFound_ThrowsNotFoundException()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act
        var act = () => _validator.ValidateDeleteAsync(nonExistentId, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("*Instructor*");
    }

    [Fact]
    public async Task ValidateDeleteAsync_WhenHasCourseGroups_ThrowsConflictException()
    {
        // Arrange
        var instructor = Instructor.Create(Guid.NewGuid(), _organizationId, "Math");
        _dbContext.Instructors.Add(instructor);

        var courseGroup = CourseGroup.Create(
            _organizationId,
            Guid.NewGuid(),
            "Group A",
            DateOnly.FromDateTime(DateTime.UtcNow),
            DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(1)),
            instructorId: instructor.Id);
        _dbContext.CourseGroups.Add(courseGroup);
        await _dbContext.SaveChangesAsync();

        // Act
        var act = () => _validator.ValidateDeleteAsync(instructor.Id, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ConflictException>()
            .WithMessage("*associated records*course group*");
    }

    [Fact]
    public async Task ValidateDeleteAsync_WhenHasSessions_ThrowsConflictException()
    {
        // Arrange
        var instructor = Instructor.Create(Guid.NewGuid(), _organizationId, "Physics");
        _dbContext.Instructors.Add(instructor);

        var session = new Session
        {
            Id = Guid.NewGuid(),
            CourseGroupId = Guid.NewGuid(),
            OrganizationId = _organizationId,
            InstructorId = instructor.Id,
            StartTime = DateTime.UtcNow,
            EndTime = DateTime.UtcNow.AddHours(2)
        };
        _dbContext.Sessions.Add(session);
        await _dbContext.SaveChangesAsync();

        // Act
        var act = () => _validator.ValidateDeleteAsync(instructor.Id, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ConflictException>()
            .WithMessage("*associated records*session*");
    }

    [Fact]
    public async Task ValidateDeleteAsync_WhenNoDependencies_DoesNotThrow()
    {
        // Arrange
        var instructor = Instructor.Create(Guid.NewGuid(), _organizationId, "Chemistry");
        _dbContext.Instructors.Add(instructor);
        await _dbContext.SaveChangesAsync();

        // Act
        var act = () => _validator.ValidateDeleteAsync(instructor.Id, CancellationToken.None);

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public void ValidateQuery_WithInvalidParameters_ThrowsValidationException()
    {
        // Arrange
        var query = new InstructorQueryParameters
        {
            PageNumber = 0,
            PageSize = 101,
            SearchTerm = new string('S', 101),
            Specialization = new string('X', 151),
            SortBy = "InvalidSortField"
        };

        // Act
        var act = () => _validator.ValidateQuery(query);

        // Assert
        act.Should().Throw<ValidationException>();
    }

    [Fact]
    public void ValidateQuery_WithValidParameters_DoesNotThrow()
    {
        // Arrange
        var query = new InstructorQueryParameters
        {
            PageNumber = 1,
            PageSize = 20,
            SearchTerm = "John",
            Specialization = "Computer Science",
            SortBy = "Specialization",
            SortDescending = true
        };

        // Act
        var act = () => _validator.ValidateQuery(query);

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public async Task ValidateLinkUserAsync_WhenInstructorIdEmpty_ThrowsValidationException()
    {
        // Act
        var act = () => _validator.ValidateLinkUserAsync(Guid.Empty, Guid.NewGuid(), _organizationId, CancellationToken.None);

        // Assert
        var ex = await act.Should().ThrowAsync<ValidationException>();
        ex.Which.Errors.Should().ContainKey("instructorId");
    }

    [Fact]
    public async Task ValidateLinkUserAsync_WhenUserIdEmpty_ThrowsValidationException()
    {
        // Act
        var act = () => _validator.ValidateLinkUserAsync(Guid.NewGuid(), Guid.Empty, _organizationId, CancellationToken.None);

        // Assert
        var ex = await act.Should().ThrowAsync<ValidationException>();
        ex.Which.Errors.Should().ContainKey("newUserId");
    }

    [Fact]
    public async Task ValidateLinkUserAsync_WhenInstructorNotFound_ThrowsNotFoundException()
    {
        // Arrange
        var nonExistentInstructorId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        // Act
        var act = () => _validator.ValidateLinkUserAsync(nonExistentInstructorId, userId, _organizationId, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("*Instructor*");
    }

    [Fact]
    public async Task ValidateLinkUserAsync_WhenUserNotFound_ThrowsNotFoundException()
    {
        // Arrange
        var instructor = Instructor.Create(Guid.NewGuid(), _organizationId, "Web Dev");
        _dbContext.Instructors.Add(instructor);
        await _dbContext.SaveChangesAsync();

        var nonExistentUserId = Guid.NewGuid();

        // Act
        var act = () => _validator.ValidateLinkUserAsync(instructor.Id, nonExistentUserId, _organizationId, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("*User*");
    }

    [Fact]
    public async Task ValidateLinkUserAsync_WhenUserInDifferentOrg_ThrowsBadRequestException()
    {
        // Arrange
        var instructor = Instructor.Create(Guid.NewGuid(), _organizationId, "Web Dev");
        _dbContext.Instructors.Add(instructor);

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            OrganizationId = Guid.NewGuid(), // different org
            FirstName = "Other",
            LastName = "Org",
            Email = "other@example.com",
            IsActive = true
        };
        _dbContext.ApplicationUsers.Add(user);
        await _dbContext.SaveChangesAsync();

        // Act
        var act = () => _validator.ValidateLinkUserAsync(instructor.Id, user.Id, _organizationId, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<BadRequestException>()
            .WithMessage("*User does not belong to the target organization*");
    }

    [Fact]
    public async Task ValidateLinkUserAsync_WhenUserInactive_ThrowsBadRequestException()
    {
        // Arrange
        var instructor = Instructor.Create(Guid.NewGuid(), _organizationId, "Web Dev");
        _dbContext.Instructors.Add(instructor);

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            OrganizationId = _organizationId,
            FirstName = "Inactive",
            LastName = "User",
            Email = "inactive@example.com",
            IsActive = false
        };
        _dbContext.ApplicationUsers.Add(user);
        await _dbContext.SaveChangesAsync();

        // Act
        var act = () => _validator.ValidateLinkUserAsync(instructor.Id, user.Id, _organizationId, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<BadRequestException>()
            .WithMessage("*Cannot link an inactive user account*");
    }

    [Fact]
    public async Task ValidateLinkUserAsync_WhenUserAlreadyLinkedToAnotherInstructor_ThrowsConflictException()
    {
        // Arrange
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            OrganizationId = _organizationId,
            FirstName = "Busy",
            LastName = "Instructor",
            Email = "busy@example.com",
            IsActive = true
        };
        _dbContext.ApplicationUsers.Add(user);

        var existingInstructor = Instructor.Create(user.Id, _organizationId, "Design");
        var targetInstructor = Instructor.Create(Guid.NewGuid(), _organizationId, "Architecture");
        _dbContext.Instructors.AddRange(existingInstructor, targetInstructor);
        await _dbContext.SaveChangesAsync();

        // Act
        var act = () => _validator.ValidateLinkUserAsync(targetInstructor.Id, user.Id, _organizationId, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ConflictException>()
            .WithMessage("*already registered as an instructor*");
    }

    [Fact]
    public async Task ValidateLinkUserAsync_WhenValid_DoesNotThrow()
    {
        // Arrange
        var instructor = Instructor.Create(Guid.NewGuid(), _organizationId, "Data Science");
        _dbContext.Instructors.Add(instructor);

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            OrganizationId = _organizationId,
            FirstName = "New",
            LastName = "Instructor",
            Email = "newinst@example.com",
            IsActive = true
        };
        _dbContext.ApplicationUsers.Add(user);
        await _dbContext.SaveChangesAsync();

        // Act
        var act = () => _validator.ValidateLinkUserAsync(instructor.Id, user.Id, _organizationId, CancellationToken.None);

        // Assert
        await act.Should().NotThrowAsync();
    }
}
