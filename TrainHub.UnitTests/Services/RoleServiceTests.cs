using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using TrainHub.Application.Common.Exceptions;
using TrainHub.Application.Features.Roles.DTOs;
using TrainHub.Application.Services;
using TrainHub.Domain.Constants;
using TrainHub.Domain.Models;
using TrainHub.Infrastructure.Data;
using TrainHub.UnitTests.Common;

namespace TrainHub.UnitTests.Services;

public class RoleServiceTests : IDisposable
{
    private readonly TrainHubDbContext _dbContext;
    private readonly Mock<RoleManager<IdentityRole<Guid>>> _roleManagerMock;
    private readonly Mock<UserManager<ApplicationUser>> _userManagerMock;
    private readonly Mock<ILogger<RoleService>> _loggerMock;
    private readonly RoleService _service;
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _clientOrgId = Guid.NewGuid();

    public RoleServiceTests()
    {
        _dbContext = TestHelpers.CreateInMemoryDbContext();

        var roleStore = new Mock<IRoleStore<IdentityRole<Guid>>>();
        _roleManagerMock = new Mock<RoleManager<IdentityRole<Guid>>>(
            roleStore.Object, null!, null!, null!, null!);

        _userManagerMock = TestHelpers.CreateMockUserManager();
        _userManagerMock.Setup(m => m.Users).Returns(_dbContext.ApplicationUsers);

        _loggerMock = new Mock<ILogger<RoleService>>();

        _service = new RoleService(
            _roleManagerMock.Object,
            _userManagerMock.Object,
            _loggerMock.Object);

        // Seed user in database
        var user = new ApplicationUser
        {
            Id = _userId,
            UserName = "testuser",
            Email = "test@example.com",
            FirstName = "Test",
            LastName = "User",
            OrganizationId = _clientOrgId,
            IsActive = true
        };
        _dbContext.ApplicationUsers.Add(user);
        _dbContext.SaveChanges();
    }

    public void Dispose()
    {
        _dbContext.Database.EnsureDeleted();
        _dbContext.Dispose();
    }

    [Fact]
    public async Task GetAllRolesAsync_ReturnsMappedRoleDtos()
    {
        // Arrange
        var roles = new List<IdentityRole<Guid>>
        {
            new() { Id = Guid.NewGuid(), Name = AppRoles.SuperAdmin },
            new() { Id = Guid.NewGuid(), Name = AppRoles.OrgAdmin }
        }.AsQueryable();

        // In-memory mock for Roles DbSet
        var roleStore = new Mock<IRoleStore<IdentityRole<Guid>>>();
        var roleManager = new Mock<RoleManager<IdentityRole<Guid>>>(
            roleStore.Object, null!, null!, null!, null!);
        roleManager.Setup(m => m.Roles).Returns(_dbContext.Roles);

        var service = new RoleService(roleManager.Object, _userManagerMock.Object, _loggerMock.Object);

        _dbContext.Roles.AddRange(
            new IdentityRole<Guid> { Id = Guid.NewGuid(), Name = AppRoles.SuperAdmin },
            new IdentityRole<Guid> { Id = Guid.NewGuid(), Name = AppRoles.OrgAdmin }
        );
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await service.GetAllRolesAsync();

        // Assert
        result.Should().HaveCount(2);
        result.Select(r => r.Name).Should().Contain(new[] { AppRoles.SuperAdmin, AppRoles.OrgAdmin });
    }

    [Fact]
    public async Task GetUserRolesAsync_WhenUserExists_ReturnsUserRoles()
    {
        // Arrange
        _userManagerMock.Setup(m => m.GetRolesAsync(It.Is<ApplicationUser>(u => u.Id == _userId)))
            .ReturnsAsync(new List<string> { AppRoles.OrgAdmin });

        // Act
        var result = await _service.GetUserRolesAsync(_userId);

        // Assert
        result.Should().NotBeNull();
        result.UserId.Should().Be(_userId);
        result.Roles.Should().ContainSingle().Which.Should().Be(AppRoles.OrgAdmin);
    }

    [Fact]
    public async Task GetUserRolesAsync_WhenUserDoesNotExist_ThrowsNotFoundException()
    {
        // Act
        var act = () => _service.GetUserRolesAsync(Guid.NewGuid());

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task AssignRoleAsync_WhenValid_AddsRoleSuccessfully()
    {
        // Arrange
        _roleManagerMock.Setup(m => m.RoleExistsAsync(AppRoles.Instructor))
            .ReturnsAsync(true);
        _userManagerMock.Setup(m => m.IsInRoleAsync(It.Is<ApplicationUser>(u => u.Id == _userId), AppRoles.Instructor))
            .ReturnsAsync(false);
        _userManagerMock.Setup(m => m.AddToRoleAsync(It.Is<ApplicationUser>(u => u.Id == _userId), AppRoles.Instructor))
            .ReturnsAsync(IdentityResult.Success);

        // Act
        await _service.AssignRoleAsync(_userId, AppRoles.Instructor);

        // Assert
        _userManagerMock.Verify(m => m.AddToRoleAsync(It.Is<ApplicationUser>(u => u.Id == _userId), AppRoles.Instructor), Times.Once);
    }

    [Fact]
    public async Task AssignRoleAsync_WhenRoleDoesNotExist_ThrowsNotFoundException()
    {
        // Arrange
        _roleManagerMock.Setup(m => m.RoleExistsAsync("NonExistentRole"))
            .ReturnsAsync(false);

        // Act
        var act = () => _service.AssignRoleAsync(_userId, "NonExistentRole");

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task AssignRoleAsync_WhenUserAlreadyInRole_ThrowsConflictException()
    {
        // Arrange
        _roleManagerMock.Setup(m => m.RoleExistsAsync(AppRoles.OrgAdmin))
            .ReturnsAsync(true);
        _userManagerMock.Setup(m => m.IsInRoleAsync(It.Is<ApplicationUser>(u => u.Id == _userId), AppRoles.OrgAdmin))
            .ReturnsAsync(true);

        // Act
        var act = () => _service.AssignRoleAsync(_userId, AppRoles.OrgAdmin);

        // Assert
        await act.Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task RemoveRoleAsync_WhenValid_RemovesRoleSuccessfully()
    {
        // Arrange
        _userManagerMock.Setup(m => m.IsInRoleAsync(It.Is<ApplicationUser>(u => u.Id == _userId), AppRoles.Instructor))
            .ReturnsAsync(true);
        _userManagerMock.Setup(m => m.RemoveFromRoleAsync(It.Is<ApplicationUser>(u => u.Id == _userId), AppRoles.Instructor))
            .ReturnsAsync(IdentityResult.Success);

        // Act
        await _service.RemoveRoleAsync(_userId, AppRoles.Instructor);

        // Assert
        _userManagerMock.Verify(m => m.RemoveFromRoleAsync(It.Is<ApplicationUser>(u => u.Id == _userId), AppRoles.Instructor), Times.Once);
    }

    [Fact]
    public async Task RemoveRoleAsync_WhenUserNotInRole_ThrowsBadRequestException()
    {
        // Arrange
        _userManagerMock.Setup(m => m.IsInRoleAsync(It.Is<ApplicationUser>(u => u.Id == _userId), AppRoles.Instructor))
            .ReturnsAsync(false);

        // Act
        var act = () => _service.RemoveRoleAsync(_userId, AppRoles.Instructor);

        // Assert
        await act.Should().ThrowAsync<BadRequestException>();
    }

    [Fact]
    public async Task AssignRoleAsync_WhenUserHasStudentRoleAndAssigningInstructor_ThrowsConflictException()
    {
        // Arrange
        _roleManagerMock.Setup(m => m.RoleExistsAsync(AppRoles.Instructor))
            .ReturnsAsync(true);
        _userManagerMock.Setup(m => m.IsInRoleAsync(It.Is<ApplicationUser>(u => u.Id == _userId), AppRoles.Instructor))
            .ReturnsAsync(false);
        _userManagerMock.Setup(m => m.IsInRoleAsync(It.Is<ApplicationUser>(u => u.Id == _userId), AppRoles.Student))
            .ReturnsAsync(true);

        // Act
        var act = () => _service.AssignRoleAsync(_userId, AppRoles.Instructor);

        // Assert
        await act.Should().ThrowAsync<ConflictException>()
            .WithMessage("*both Instructor and Student roles simultaneously*");
    }

    [Fact]
    public async Task AssignRoleAsync_WhenUserHasInstructorRoleAndAssigningStudent_ThrowsConflictException()
    {
        // Arrange
        _roleManagerMock.Setup(m => m.RoleExistsAsync(AppRoles.Student))
            .ReturnsAsync(true);
        _userManagerMock.Setup(m => m.IsInRoleAsync(It.Is<ApplicationUser>(u => u.Id == _userId), AppRoles.Student))
            .ReturnsAsync(false);
        _userManagerMock.Setup(m => m.IsInRoleAsync(It.Is<ApplicationUser>(u => u.Id == _userId), AppRoles.Instructor))
            .ReturnsAsync(true);

        // Act
        var act = () => _service.AssignRoleAsync(_userId, AppRoles.Student);

        // Assert
        await act.Should().ThrowAsync<ConflictException>()
            .WithMessage("*both Student and Instructor roles simultaneously*");
    }

    [Fact]
    public async Task GetPlatformUsersAsync_ReturnsOnlyUsersWithSuperAdminRole()
    {
        // Arrange
        var superAdmin = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = "superadmin",
            Email = "admin@platform.io",
            FirstName = "Super",
            LastName = "Admin",
            IsActive = true
        };

        _userManagerMock.Setup(m => m.GetUsersInRoleAsync(AppRoles.SuperAdmin))
            .ReturnsAsync(new List<ApplicationUser> { superAdmin });

        // Act
        var result = await _service.GetPlatformUsersAsync();

        // Assert
        result.Should().NotBeNull();
        result.Should().ContainSingle(u => u.Email == "admin@platform.io" && u.Role == "Platform Super Admin");
        _userManagerMock.Verify(m => m.GetUsersInRoleAsync(AppRoles.SuperAdmin), Times.Once);
    }
}
