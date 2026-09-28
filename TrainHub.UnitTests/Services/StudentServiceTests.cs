using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using TrainHub.Application.Common.Exceptions;
using TrainHub.Application.Features.Students.DTOs;
using TrainHub.Application.Interfaces;
using TrainHub.Domain.Models;
using TrainHub.Infrastructure;
using TrainHub.Infrastructure.Data;
using TrainHub.Infrastructure.Services;
using TrainHub.UnitTests.Common;

namespace TrainHub.UnitTests.Services;

public class StudentServiceTests : IDisposable
{
    private readonly TrainHubDbContext _dbContext;
    private readonly IUnitOfWork _unitOfWork;
    private readonly Mock<IStudentValidator> _studentValidatorMock;
    private readonly Mock<ICurrentOrganizationService> _currentOrgServiceMock;
    private readonly Mock<ILogger<StudentService>> _loggerMock;
    private readonly StudentService _service;
    private readonly Guid _organizationId = Guid.NewGuid();

    public StudentServiceTests()
    {
        _dbContext = TestHelpers.CreateInMemoryDbContext();
        _unitOfWork = new UnitOfWork(_dbContext);
        _studentValidatorMock = new Mock<IStudentValidator>();
        _currentOrgServiceMock = new Mock<ICurrentOrganizationService>();
        _currentOrgServiceMock.Setup(s => s.OrganizationId).Returns(_organizationId);
        _loggerMock = new Mock<ILogger<StudentService>>();

        _service = new StudentService(
            _unitOfWork,
            _studentValidatorMock.Object,
            _currentOrgServiceMock.Object,
            _dbContext,
            _loggerMock.Object);
    }

    public void Dispose()
    {
        _dbContext.Database.EnsureDeleted();
        _dbContext.Dispose();
        _unitOfWork.Dispose();
    }

    [Fact]
    public async Task CreateAsync_ValidDto_CreatesAndReturnsStudentDto()
    {
        // Arrange
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            OrganizationId = _organizationId,
            FirstName = "Alice",
            LastName = "Smith",
            Email = "alice@example.com"
        };
        await _dbContext.ApplicationUsers.AddAsync(user);
        await _dbContext.SaveChangesAsync();

        var dto = new CreateStudentDto
        {
            UserId = user.Id,
            StudentCode = "STU-001",
            GuardianPhone = "+1234567890",
            AcademicYear = "Grade 10"
        };

        // Act
        var result = await _service.CreateAsync(dto, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.UserId.Should().Be(user.Id);
        result.OrganizationId.Should().Be(_organizationId);
        result.StudentCode.Should().Be(dto.StudentCode);
        result.FirstName.Should().Be("Alice");
        result.LastName.Should().Be("Smith");
        result.Email.Should().Be("alice@example.com");

        _studentValidatorMock.Verify(v => v.ValidateCreateAsync(dto, _organizationId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_WhenNoOrgIdAndContextEmpty_ThrowsBadRequestException()
    {
        // Arrange
        _currentOrgServiceMock.Setup(s => s.OrganizationId).Returns((Guid?)null);
        var dto = new CreateStudentDto
        {
            UserId = Guid.NewGuid(),
            StudentCode = "STU-001"
        };

        // Act
        var act = () => _service.CreateAsync(dto, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<BadRequestException>()
            .WithMessage("*Organization ID must be provided*");
    }

    [Fact]
    public async Task GetByIdAsync_WhenStudentNotFound_ThrowsNotFoundException()
    {
        // Act
        var act = () => _service.GetByIdAsync(Guid.NewGuid(), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task GetByIdAsync_WhenStudentExists_ReturnsStudentDto()
    {
        // Arrange
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            OrganizationId = _organizationId,
            FirstName = "Bob",
            LastName = "Johnson",
            Email = "bob@example.com"
        };
        await _dbContext.ApplicationUsers.AddAsync(user);

        var student = Student.Create(user.Id, _organizationId, "STU-BOB", "+1122334455");
        await _dbContext.Students.AddAsync(student);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.GetByIdAsync(student.Id, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(student.Id);
        result.StudentCode.Should().Be("STU-BOB");
        result.FirstName.Should().Be("Bob");
    }

    [Fact]
    public async Task GetAllAsync_ReturnsAllStudents()
    {
        // Arrange
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            OrganizationId = _organizationId,
            FirstName = "Charlie",
            LastName = "Brown",
            Email = "charlie@example.com"
        };
        await _dbContext.ApplicationUsers.AddAsync(user);

        var student = Student.Create(user.Id, _organizationId, "STU-CHARLIE");
        await _dbContext.Students.AddAsync(student);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.GetAllAsync(CancellationToken.None);

        // Assert
        result.Should().ContainSingle(s => s.StudentCode == "STU-CHARLIE");
    }

    [Fact]
    public async Task UpdateAsync_WhenStudentNotFound_ThrowsNotFoundException()
    {
        // Act
        var act = () => _service.UpdateAsync(Guid.NewGuid(), new UpdateStudentDto { StudentCode = "STU-UPDATED" }, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task UpdateAsync_WhenValid_UpdatesAndReturnsDto()
    {
        // Arrange
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            OrganizationId = _organizationId,
            FirstName = "David",
            LastName = "Miller",
            Email = "david@example.com"
        };
        await _dbContext.ApplicationUsers.AddAsync(user);

        var student = Student.Create(user.Id, _organizationId, "STU-OLD");
        await _dbContext.Students.AddAsync(student);
        await _dbContext.SaveChangesAsync();

        var updateDto = new UpdateStudentDto
        {
            StudentCode = "STU-NEW",
            GuardianPhone = "+9988776655",
            AcademicYear = "Senior",
            Notes = "Honor student"
        };

        // Act
        var result = await _service.UpdateAsync(student.Id, updateDto, CancellationToken.None);

        // Assert
        result.StudentCode.Should().Be("STU-NEW");
        result.GuardianPhone.Should().Be("+9988776655");
        result.AcademicYear.Should().Be("Senior");
        result.Notes.Should().Be("Honor student");

        _studentValidatorMock.Verify(v => v.ValidateUpdateAsync(student.Id, updateDto, _organizationId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteAsync_WhenStudentNotFound_ThrowsNotFoundException()
    {
        // Act
        var act = () => _service.DeleteAsync(Guid.NewGuid(), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task DeleteAsync_WhenValid_DeletesStudent()
    {
        // Arrange
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            OrganizationId = _organizationId,
            FirstName = "Eve",
            LastName = "Wilson",
            Email = "eve@example.com"
        };
        await _dbContext.ApplicationUsers.AddAsync(user);

        var student = Student.Create(user.Id, _organizationId, "STU-EVE");
        await _dbContext.Students.AddAsync(student);
        await _dbContext.SaveChangesAsync();

        // Act
        await _service.DeleteAsync(student.Id, CancellationToken.None);

        // Assert
        var deleted = await _dbContext.Students.FindAsync(student.Id);
        deleted.Should().BeNull();

        _studentValidatorMock.Verify(v => v.ValidateDeleteAsync(student.Id, It.IsAny<CancellationToken>()), Times.Once);
    }
}
