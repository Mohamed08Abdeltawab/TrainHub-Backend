using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Moq;
using TrainHub.Application.Common.Exceptions;
using TrainHub.Application.Features.Instructors.DTOs;
using TrainHub.Application.Interfaces;
using TrainHub.Domain.Constants;
using TrainHub.Domain.Models;
using TrainHub.Infrastructure;
using TrainHub.Infrastructure.Data;
using TrainHub.Infrastructure.Services;
using TrainHub.UnitTests.Common;

namespace TrainHub.UnitTests.Services;

public class InstructorServiceTests : IDisposable
{
    private readonly TrainHubDbContext _dbContext;
    private readonly IUnitOfWork _unitOfWork;
    private readonly Mock<IInstructorValidator> _instructorValidatorMock;
    private readonly Mock<ICurrentOrganizationService> _currentOrgServiceMock;
    private readonly Mock<UserManager<ApplicationUser>> _userManagerMock;
    private readonly Mock<ILogger<InstructorService>> _loggerMock;
    private readonly InstructorService _service;
    private readonly Guid _organizationId = Guid.NewGuid();

    public InstructorServiceTests()
    {
        _dbContext = TestHelpers.CreateInMemoryDbContext();
        _unitOfWork = new UnitOfWork(_dbContext);
        _instructorValidatorMock = new Mock<IInstructorValidator>();
        _currentOrgServiceMock = new Mock<ICurrentOrganizationService>();
        _currentOrgServiceMock.Setup(s => s.OrganizationId).Returns(_organizationId);
        _userManagerMock = TestHelpers.CreateMockUserManager();
        _userManagerMock.Setup(m => m.FindByIdAsync(It.IsAny<string>()))
            .ReturnsAsync((string id) => _dbContext.ApplicationUsers.Find(Guid.Parse(id)));
        _userManagerMock.Setup(m => m.IsInRoleAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()))
            .ReturnsAsync(false);
        _userManagerMock.Setup(m => m.AddToRoleAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()))
            .ReturnsAsync(IdentityResult.Success);
        _userManagerMock.Setup(m => m.RemoveFromRoleAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()))
            .ReturnsAsync(IdentityResult.Success);
        _loggerMock = new Mock<ILogger<InstructorService>>();

        _service = new InstructorService(
            _unitOfWork,
            _instructorValidatorMock.Object,
            _currentOrgServiceMock.Object,
            _dbContext,
            _userManagerMock.Object,
            _loggerMock.Object);
    }

    public void Dispose()
    {
        _dbContext.Database.EnsureDeleted();
        _dbContext.Dispose();
        _unitOfWork.Dispose();
    }

    [Fact]
    public async Task CreateAsync_ValidDto_CreatesAndReturnsInstructorDto()
    {
        // Arrange
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            OrganizationId = _organizationId,
            FirstName = "John",
            LastName = "Doe",
            Email = "john.doe@example.com",
            PhoneNumber = "+1234567890"
        };
        _dbContext.ApplicationUsers.Add(user);
        await _dbContext.SaveChangesAsync();

        var dto = new CreateInstructorDto
        {
            UserId = user.Id,
            Specialization = "Software Engineering",
            Bio = "Senior Engineer"
        };

        // Act
        var result = await _service.CreateAsync(dto, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.UserId.Should().Be(user.Id);
        result.OrganizationId.Should().Be(_organizationId);
        result.Specialization.Should().Be("Software Engineering");
        result.Bio.Should().Be("Senior Engineer");
        result.FirstName.Should().Be("John");
        result.LastName.Should().Be("Doe");
        result.Email.Should().Be("john.doe@example.com");

        _instructorValidatorMock.Verify(v => v.ValidateCreateAsync(dto, _organizationId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_WhenOrgIdCannotBeDetermined_ThrowsBadRequestException()
    {
        // Arrange
        _currentOrgServiceMock.Setup(s => s.OrganizationId).Returns((Guid?)null);

        var dto = new CreateInstructorDto
        {
            UserId = Guid.NewGuid(),
            Specialization = "Math"
        };

        // Act
        var act = () => _service.CreateAsync(dto, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<BadRequestException>()
            .WithMessage("*Organization ID must be provided*");
    }

    [Fact]
    public async Task GetByIdAsync_WhenExists_ReturnsInstructorDto()
    {
        // Arrange
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            OrganizationId = _organizationId,
            FirstName = "Jane",
            LastName = "Smith",
            Email = "jane.smith@example.com"
        };
        _dbContext.ApplicationUsers.Add(user);

        var instructor = Instructor.Create(user.Id, _organizationId, "Artificial Intelligence", "AI Expert");
        _dbContext.Instructors.Add(instructor);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.GetByIdAsync(instructor.Id, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(instructor.Id);
        result.Specialization.Should().Be("Artificial Intelligence");
        result.FirstName.Should().Be("Jane");
    }

    [Fact]
    public async Task GetByIdAsync_WhenNotFound_ThrowsNotFoundException()
    {
        // Act
        var act = () => _service.GetByIdAsync(Guid.NewGuid(), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("*Instructor*");
    }

    [Fact]
    public async Task GetAllAsync_ReturnsAllInstructors()
    {
        // Arrange
        var user1 = new ApplicationUser { Id = Guid.NewGuid(), OrganizationId = _organizationId, FirstName = "A", LastName = "User" };
        var user2 = new ApplicationUser { Id = Guid.NewGuid(), OrganizationId = _organizationId, FirstName = "B", LastName = "User" };
        _dbContext.ApplicationUsers.AddRange(user1, user2);

        var inst1 = Instructor.Create(user1.Id, _organizationId, "Spec A");
        var inst2 = Instructor.Create(user2.Id, _organizationId, "Spec B");
        _dbContext.Instructors.AddRange(inst1, inst2);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.GetAllAsync(CancellationToken.None);

        // Assert
        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetPagedAsync_WithSearchTerm_ReturnsFilteredPagedResult()
    {
        // Arrange
        var user1 = new ApplicationUser { Id = Guid.NewGuid(), OrganizationId = _organizationId, FirstName = "Alan", LastName = "Turing", Email = "alan@example.com" };
        var user2 = new ApplicationUser { Id = Guid.NewGuid(), OrganizationId = _organizationId, FirstName = "Grace", LastName = "Hopper", Email = "grace@example.com" };
        _dbContext.ApplicationUsers.AddRange(user1, user2);

        var inst1 = Instructor.Create(user1.Id, _organizationId, "Theoretical CS");
        var inst2 = Instructor.Create(user2.Id, _organizationId, "Compiler Design");
        _dbContext.Instructors.AddRange(inst1, inst2);
        await _dbContext.SaveChangesAsync();

        var query = new InstructorQueryParameters { SearchTerm = "Hopper" };

        // Act
        var result = await _service.GetPagedAsync(query, CancellationToken.None);

        // Assert
        result.TotalCount.Should().Be(1);
        result.Items.Should().ContainSingle(i => i.LastName == "Hopper");
    }

    [Fact]
    public async Task GetPagedAsync_WithSpecializationFilter_ReturnsFilteredPagedResult()
    {
        // Arrange
        var user1 = new ApplicationUser { Id = Guid.NewGuid(), OrganizationId = _organizationId, FirstName = "A", LastName = "A" };
        var user2 = new ApplicationUser { Id = Guid.NewGuid(), OrganizationId = _organizationId, FirstName = "B", LastName = "B" };
        _dbContext.ApplicationUsers.AddRange(user1, user2);

        var inst1 = Instructor.Create(user1.Id, _organizationId, "Frontend React");
        var inst2 = Instructor.Create(user2.Id, _organizationId, "Backend ASP.NET");
        _dbContext.Instructors.AddRange(inst1, inst2);
        await _dbContext.SaveChangesAsync();

        var query = new InstructorQueryParameters { Specialization = "React" };

        // Act
        var result = await _service.GetPagedAsync(query, CancellationToken.None);

        // Assert
        result.TotalCount.Should().Be(1);
        result.Items.Should().ContainSingle(i => i.Specialization == "Frontend React");
    }

    [Fact]
    public async Task UpdateAsync_WhenExists_UpdatesAndReturnsInstructorDto()
    {
        // Arrange
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            OrganizationId = _organizationId,
            FirstName = "Ada",
            LastName = "Lovelace"
        };
        _dbContext.ApplicationUsers.Add(user);

        var instructor = Instructor.Create(user.Id, _organizationId, "Algorithms", "Initial bio");
        _dbContext.Instructors.Add(instructor);
        await _dbContext.SaveChangesAsync();

        var updateDto = new UpdateInstructorDto
        {
            Specialization = "Advanced Algorithms & Optimization",
            Bio = "Updated bio content"
        };

        // Act
        var result = await _service.UpdateAsync(instructor.Id, updateDto, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Specialization.Should().Be("Advanced Algorithms & Optimization");
        result.Bio.Should().Be("Updated bio content");

        _instructorValidatorMock.Verify(v => v.ValidateUpdateAsync(instructor.Id, updateDto, _organizationId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_WhenNotFound_ThrowsNotFoundException()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();
        var updateDto = new UpdateInstructorDto { Specialization = "Math" };

        // Act
        var act = () => _service.UpdateAsync(nonExistentId, updateDto, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("*Instructor*");
    }

    [Fact]
    public async Task DeleteAsync_WhenValid_DeletesInstructor()
    {
        // Arrange
        var instructor = Instructor.Create(Guid.NewGuid(), _organizationId, "Physics");
        _dbContext.Instructors.Add(instructor);
        await _dbContext.SaveChangesAsync();

        // Act
        await _service.DeleteAsync(instructor.Id, CancellationToken.None);

        // Assert
        var deleted = await _dbContext.Instructors.FindAsync(instructor.Id);
        deleted.Should().BeNull();

        _instructorValidatorMock.Verify(v => v.ValidateDeleteAsync(instructor.Id, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteAsync_WhenNotFound_ThrowsNotFoundException()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act
        var act = () => _service.DeleteAsync(nonExistentId, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("*Instructor*");
    }

    [Fact]
    public async Task LinkUserAsync_WhenValid_UpdatesUserIdAndAssignsRole()
    {
        // Arrange
        var initialUser = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            OrganizationId = _organizationId,
            FirstName = "Initial",
            LastName = "User"
        };
        var newUser = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            OrganizationId = _organizationId,
            FirstName = "Linked",
            LastName = "User",
            Email = "linked@example.com"
        };
        _dbContext.ApplicationUsers.AddRange(initialUser, newUser);

        var instructor = Instructor.Create(initialUser.Id, _organizationId, "Robotics");
        _dbContext.Instructors.Add(instructor);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.LinkUserAsync(instructor.Id, newUser.Id, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(instructor.Id);
        result.UserId.Should().Be(newUser.Id);
        result.FirstName.Should().Be("Linked");
        result.LastName.Should().Be("User");

        var updatedInstructor = await _dbContext.Instructors.FindAsync(instructor.Id);
        updatedInstructor!.UserId.Should().Be(newUser.Id);

        _instructorValidatorMock.Verify(v => v.ValidateLinkUserAsync(instructor.Id, newUser.Id, _organizationId, It.IsAny<CancellationToken>()), Times.Once);
        _userManagerMock.Verify(m => m.AddToRoleAsync(It.Is<ApplicationUser>(u => u.Id == newUser.Id), AppRoles.Instructor), Times.Once);
    }

    [Fact]
    public async Task LinkUserAsync_WhenRelinkingDifferentUser_RemovesRoleFromOldUserAndAddsToNewUser()
    {
        // Arrange
        var oldUser = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            OrganizationId = _organizationId,
            FirstName = "Old",
            LastName = "User"
        };
        var newUser = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            OrganizationId = _organizationId,
            FirstName = "New",
            LastName = "User"
        };
        _dbContext.ApplicationUsers.AddRange(oldUser, newUser);

        var instructor = Instructor.Create(oldUser.Id, _organizationId, "Quantum Computing");
        _dbContext.Instructors.Add(instructor);
        await _dbContext.SaveChangesAsync();

        _userManagerMock.Setup(m => m.IsInRoleAsync(It.Is<ApplicationUser>(u => u.Id == oldUser.Id), AppRoles.Instructor))
            .ReturnsAsync(true);

        // Act
        await _service.LinkUserAsync(instructor.Id, newUser.Id, CancellationToken.None);

        // Assert
        _userManagerMock.Verify(m => m.RemoveFromRoleAsync(It.Is<ApplicationUser>(u => u.Id == oldUser.Id), AppRoles.Instructor), Times.Once);
        _userManagerMock.Verify(m => m.AddToRoleAsync(It.Is<ApplicationUser>(u => u.Id == newUser.Id), AppRoles.Instructor), Times.Once);
    }

    [Fact]
    public async Task LinkUserAsync_WithDto_DelegatesAndSucceeds()
    {
        // Arrange
        var initialUser = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            OrganizationId = _organizationId,
            FirstName = "Initial",
            LastName = "User"
        };
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            OrganizationId = _organizationId,
            FirstName = "DTO",
            LastName = "User"
        };
        _dbContext.ApplicationUsers.AddRange(initialUser, user);

        var instructor = Instructor.Create(initialUser.Id, _organizationId, "Embedded Systems");
        _dbContext.Instructors.Add(instructor);
        await _dbContext.SaveChangesAsync();

        var dto = new LinkInstructorUserDto { UserId = user.Id };

        // Act
        var result = await _service.LinkUserAsync(instructor.Id, dto, CancellationToken.None);

        // Assert
        result.UserId.Should().Be(user.Id);
    }

    [Fact]
    public async Task LinkUserAsync_WhenNotFound_ThrowsNotFoundException()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act
        var act = () => _service.LinkUserAsync(nonExistentId, Guid.NewGuid(), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("*Instructor*");
    }

    [Fact]
    public async Task GetByUserIdAsync_WhenExists_ReturnsInstructorDto()
    {
        // Arrange
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            OrganizationId = _organizationId,
            FirstName = "Find",
            LastName = "Me"
        };
        _dbContext.ApplicationUsers.Add(user);

        var instructor = Instructor.Create(user.Id, _organizationId, "Database Systems");
        _dbContext.Instructors.Add(instructor);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.GetByUserIdAsync(user.Id, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(instructor.Id);
        result.UserId.Should().Be(user.Id);
        result.Specialization.Should().Be("Database Systems");
    }

    [Fact]
    public async Task GetByUserIdAsync_WhenNotFound_ThrowsNotFoundException()
    {
        // Act
        var act = () => _service.GetByUserIdAsync(Guid.NewGuid(), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("*Instructor*");
    }
}
