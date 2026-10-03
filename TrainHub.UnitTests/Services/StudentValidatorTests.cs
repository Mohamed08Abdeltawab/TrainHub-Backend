using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using TrainHub.Application.Common.Exceptions;
using TrainHub.Application.Features.Students.DTOs;
using TrainHub.Domain.Models;
using TrainHub.Infrastructure.Data;
using TrainHub.Application.Services;
using TrainHub.UnitTests.Common;

namespace TrainHub.UnitTests.Services;

public class StudentValidatorTests : IDisposable
{
    private readonly TrainHubDbContext _dbContext;
    private readonly Mock<ILogger<StudentValidator>> _loggerMock;
    private readonly StudentValidator _validator;
    private readonly Guid _organizationId = Guid.NewGuid();

    public StudentValidatorTests()
    {
        _dbContext = TestHelpers.CreateInMemoryDbContext();
        _loggerMock = new Mock<ILogger<StudentValidator>>();
        _validator = new StudentValidator(_dbContext, _loggerMock.Object);
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
        var dto = new CreateStudentDto
        {
            UserId = Guid.NewGuid(),
            StudentCode = "STU-001"
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
            Email = "test@example.com"
        };
        await _dbContext.ApplicationUsers.AddAsync(user);
        await _dbContext.SaveChangesAsync();

        var dto = new CreateStudentDto
        {
            UserId = user.Id,
            StudentCode = "STU-001"
        };

        // Act
        var act = () => _validator.ValidateCreateAsync(dto, _organizationId, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<BadRequestException>()
            .WithMessage("*User does not belong to the target organization*");
    }

    [Fact]
    public async Task ValidateCreateAsync_WhenUserAlreadyHasStudent_ThrowsConflictException()
    {
        // Arrange
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            OrganizationId = _organizationId,
            FirstName = "Test",
            LastName = "User",
            Email = "test@example.com"
        };
        await _dbContext.ApplicationUsers.AddAsync(user);

        var existingStudent = Student.Create(user.Id, _organizationId, "STU-EXISTING");
        await _dbContext.Students.AddAsync(existingStudent);
        await _dbContext.SaveChangesAsync();

        var dto = new CreateStudentDto
        {
            UserId = user.Id,
            StudentCode = "STU-NEW"
        };

        // Act
        var act = () => _validator.ValidateCreateAsync(dto, _organizationId, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ConflictException>()
            .WithMessage("*already registered as a student*");
    }

    [Fact]
    public async Task ValidateCreateAsync_WhenStudentCodeAlreadyExists_ThrowsConflictException()
    {
        // Arrange
        var user1 = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            OrganizationId = _organizationId,
            FirstName = "User",
            LastName = "One",
            Email = "one@example.com"
        };
        var user2 = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            OrganizationId = _organizationId,
            FirstName = "User",
            LastName = "Two",
            Email = "two@example.com"
        };
        await _dbContext.ApplicationUsers.AddRangeAsync(user1, user2);

        var existingStudent = Student.Create(user1.Id, _organizationId, "STU-DUPLICATE");
        await _dbContext.Students.AddAsync(existingStudent);
        await _dbContext.SaveChangesAsync();

        var dto = new CreateStudentDto
        {
            UserId = user2.Id,
            StudentCode = "STU-DUPLICATE"
        };

        // Act
        var act = () => _validator.ValidateCreateAsync(dto, _organizationId, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ConflictException>()
            .WithMessage("*already exists in this organization*");
    }

    [Fact]
    public async Task ValidateUpdateAsync_WhenStudentNotFound_ThrowsNotFoundException()
    {
        // Arrange
        var dto = new UpdateStudentDto { StudentCode = "STU-NEW" };

        // Act
        var act = () => _validator.ValidateUpdateAsync(Guid.NewGuid(), dto, _organizationId, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task ValidateUpdateAsync_WhenCodeUsedByAnotherStudent_ThrowsConflictException()
    {
        // Arrange
        var user1 = new ApplicationUser { Id = Guid.NewGuid(), OrganizationId = _organizationId, FirstName = "A", LastName = "B" };
        var user2 = new ApplicationUser { Id = Guid.NewGuid(), OrganizationId = _organizationId, FirstName = "C", LastName = "D" };
        await _dbContext.ApplicationUsers.AddRangeAsync(user1, user2);

        var student1 = Student.Create(user1.Id, _organizationId, "STU-001");
        var student2 = Student.Create(user2.Id, _organizationId, "STU-002");
        await _dbContext.Students.AddRangeAsync(student1, student2);
        await _dbContext.SaveChangesAsync();

        var updateDto = new UpdateStudentDto { StudentCode = "STU-002" }; // trying to use student2's code

        // Act
        var act = () => _validator.ValidateUpdateAsync(student1.Id, updateDto, _organizationId, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ConflictException>()
            .WithMessage("*already exists in this organization*");
    }

    [Fact]
    public async Task ValidateDeleteAsync_WhenHasEnrollments_ThrowsConflictException()
    {
        // Arrange
        var user = new ApplicationUser { Id = Guid.NewGuid(), OrganizationId = _organizationId, FirstName = "A", LastName = "B" };
        await _dbContext.ApplicationUsers.AddAsync(user);

        var student = Student.Create(user.Id, _organizationId, "STU-ENROLLED");
        await _dbContext.Students.AddAsync(student);

        var enrollment = Enrollment.Create(_organizationId, Guid.NewGuid(), student.Id);
        await _dbContext.Enrollments.AddAsync(enrollment);
        await _dbContext.SaveChangesAsync();

        // Act
        var act = () => _validator.ValidateDeleteAsync(student.Id, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ConflictException>()
            .WithMessage("*Cannot delete student*associated records*");
    }

    [Fact]
    public async Task ValidateDeleteAsync_WhenNoDependencies_CompletesSuccessfully()
    {
        // Arrange
        var user = new ApplicationUser { Id = Guid.NewGuid(), OrganizationId = _organizationId, FirstName = "A", LastName = "B" };
        await _dbContext.ApplicationUsers.AddAsync(user);

        var student = Student.Create(user.Id, _organizationId, "STU-CLEAN");
        await _dbContext.Students.AddAsync(student);
        await _dbContext.SaveChangesAsync();

        // Act
        var act = () => _validator.ValidateDeleteAsync(student.Id, CancellationToken.None);

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public void ValidateQuery_WithInvalidSortField_ThrowsValidationException()
    {
        // Arrange
        var query = new StudentQueryParameters { SortBy = "NonExistentField" };

        // Act
        var act = () => _validator.ValidateQuery(query);

        // Assert
        act.Should().Throw<ValidationException>()
            .Which.Errors.Should().ContainKey(nameof(StudentQueryParameters.SortBy));
    }

    [Fact]
    public void ValidateQuery_WithDobFromAfterDobTo_ThrowsValidationException()
    {
        // Arrange
        var query = new StudentQueryParameters
        {
            DateOfBirthFrom = new DateOnly(2010, 1, 1),
            DateOfBirthTo = new DateOnly(2005, 1, 1)
        };

        // Act
        var act = () => _validator.ValidateQuery(query);

        // Assert
        act.Should().Throw<ValidationException>()
            .Which.Errors.Should().ContainKey(nameof(StudentQueryParameters.DateOfBirthFrom));
    }

    [Fact]
    public void ValidateQuery_WithValidParameters_CompletesSuccessfully()
    {
        // Arrange
        var query = new StudentQueryParameters
        {
            PageNumber = 1,
            PageSize = 20,
            SearchTerm = "John",
            SortBy = "StudentCode",
            SortDescending = true
        };

        // Act
        var act = () => _validator.ValidateQuery(query);

        // Assert
        act.Should().NotThrow();
    }
}
