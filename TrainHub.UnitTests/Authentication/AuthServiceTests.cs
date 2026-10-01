using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using TrainHub.Application.Common.Exceptions;
using TrainHub.Application.Common.Security;
using TrainHub.Application.Features.Auth.DTOs;
using TrainHub.Application.Interfaces;
using TrainHub.Domain.Constants;
using TrainHub.Domain.Models;
using TrainHub.Infrastructure.Data;
using TrainHub.Application.Services;
using TrainHub.UnitTests.Common;

namespace TrainHub.UnitTests.Authentication;

public class AuthServiceTests
{
    private readonly Mock<UserManager<ApplicationUser>> _userManagerMock;
    private readonly Mock<IJwtTokenGenerator> _jwtTokenGeneratorMock;
    private readonly Mock<IOrganizationValidator> _organizationValidatorMock;
    private readonly Mock<ILogger<AuthService>> _loggerMock;
    private readonly IOptions<JwtSettings> _jwtOptions;
    private readonly TrainHubDbContext _dbContext;

    public AuthServiceTests()
    {
        _userManagerMock = TestHelpers.CreateMockUserManager();
        _jwtTokenGeneratorMock = new Mock<IJwtTokenGenerator>();
        _organizationValidatorMock = new Mock<IOrganizationValidator>();
        _loggerMock = new Mock<ILogger<AuthService>>();
        _jwtOptions = Options.Create(new JwtSettings
        {
            Secret = "a-very-long-secret-key-that-exceeds-32-bytes-in-length!",
            Issuer = "TrainHub",
            Audience = "TrainHubAudience",
            ExpiryMinutes = 60,
            RefreshTokenExpiryDays = 7
        });

        _dbContext = TestHelpers.CreateInMemoryDbContext();
    }

    private AuthService CreateService()
    {
        return new AuthService(
            _userManagerMock.Object,
            _jwtTokenGeneratorMock.Object,
            _dbContext,
            _jwtOptions,
            _organizationValidatorMock.Object,
            _loggerMock.Object);
    }

    #region RegisterAsync Tests

    [Fact]
    public async Task RegisterAsync_WhenOrganizationIsInvalidOrInactive_ThrowsException()
    {
        // Arrange
        var request = new RegisterRequest
        {
            FirstName = "John",
            LastName = "Doe",
            Email = "john@example.com",
            Password = "Password123!",
            ConfirmPassword = "Password123!",
            OrganizationId = Guid.NewGuid()
        };

        _organizationValidatorMock
            .Setup(v => v.ValidateOrganizationActiveAsync(request.OrganizationId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new BadRequestException("Organization is currently inactive."));

        var service = CreateService();

        // Act
        var act = () => service.RegisterAsync(request);

        // Assert
        await act.Should().ThrowAsync<BadRequestException>()
            .WithMessage("Organization is currently inactive.");
    }

    [Fact]
    public async Task RegisterAsync_WhenEmailAlreadyExistsInSameOrganization_ThrowsConflictException()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var request = new RegisterRequest
        {
            FirstName = "Alice",
            LastName = "Smith",
            Email = "alice@example.com",
            Password = "Password123!",
            ConfirmPassword = "Password123!",
            OrganizationId = orgId
        };

        var existingUser = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            FirstName = "Alice",
            LastName = "Smith",
            Email = request.Email,
            NormalizedEmail = request.Email.ToUpperInvariant(),
            OrganizationId = orgId
        };

        _dbContext.ApplicationUsers.Add(existingUser);
        await _dbContext.SaveChangesAsync();

        var service = CreateService();

        // Act
        var act = () => service.RegisterAsync(request);

        // Assert
        await act.Should().ThrowAsync<ConflictException>()
            .WithMessage($"*{request.Email}*already exists*");
    }

    [Fact]
    public async Task RegisterAsync_WhenIdentityCreationFails_ThrowsBadRequestException()
    {
        // Arrange
        var request = new RegisterRequest
        {
            FirstName = "Bob",
            LastName = "Brown",
            Email = "bob@example.com",
            Password = "Weak",
            ConfirmPassword = "Weak",
            OrganizationId = Guid.NewGuid()
        };

        _userManagerMock.Setup(m => m.FindByEmailAsync(request.Email))
            .ReturnsAsync((ApplicationUser?)null);

        var identityError = new IdentityError { Description = "Password is too weak." };
        _userManagerMock.Setup(m => m.CreateAsync(It.IsAny<ApplicationUser>(), request.Password))
            .ReturnsAsync(IdentityResult.Failed(identityError));

        var service = CreateService();

        // Act
        var act = () => service.RegisterAsync(request);

        // Assert
        await act.Should().ThrowAsync<BadRequestException>()
            .WithMessage("*Password is too weak*");
    }

    [Fact]
    public async Task RegisterAsync_WhenValid_CreatesUserAndReturnsAuthResponse()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var request = new RegisterRequest
        {
            FirstName = "Charlie",
            LastName = "Day",
            Email = "charlie@example.com",
            Password = "StrongPassword123!",
            ConfirmPassword = "StrongPassword123!",
            OrganizationId = orgId,
            PhoneNumber = "+1234567890"
        };

        _userManagerMock.Setup(m => m.FindByEmailAsync(request.Email))
            .ReturnsAsync((ApplicationUser?)null);

        _userManagerMock.Setup(m => m.CreateAsync(It.IsAny<ApplicationUser>(), request.Password))
            .ReturnsAsync(IdentityResult.Success);

        _userManagerMock.Setup(m => m.AddToRoleAsync(It.IsAny<ApplicationUser>(), AppRoles.Student))
            .ReturnsAsync(IdentityResult.Success);

        _jwtTokenGeneratorMock.Setup(g => g.GenerateToken(It.IsAny<ApplicationUser>(), It.IsAny<IEnumerable<string>>(), null))
            .Returns("fake-jwt-token");

        _jwtTokenGeneratorMock.Setup(g => g.GenerateRefreshToken())
            .Returns("fake-refresh-token");

        _userManagerMock.Setup(m => m.UpdateAsync(It.IsAny<ApplicationUser>()))
            .ReturnsAsync(IdentityResult.Success);

        var service = CreateService();

        // Act
        var result = await service.RegisterAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Email.Should().Be(request.Email);
        result.FirstName.Should().Be(request.FirstName);
        result.LastName.Should().Be(request.LastName);
        result.AccessToken.Should().Be("fake-jwt-token");
        result.RefreshToken.Should().Be("fake-refresh-token");
        result.Roles.Should().Contain(AppRoles.Student);

        _userManagerMock.Verify(m => m.AddToRoleAsync(It.IsAny<ApplicationUser>(), AppRoles.Student), Times.Once);
        _userManagerMock.Verify(m => m.UpdateAsync(It.Is<ApplicationUser>(u => u.RefreshToken == "fake-refresh-token")), Times.Once);
    }

    #endregion

    #region LoginAsync Tests

    [Fact]
    public async Task LoginAsync_WhenUserNotFoundInOrganization_ThrowsUnauthorizedAccessException()
    {
        // Arrange
        var request = new LoginRequest
        {
            Email = "missing@example.com",
            Password = "Password123!",
            OrganizationId = Guid.NewGuid()
        };

        var service = CreateService();

        // Act
        var act = () => service.LoginAsync(request);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("Invalid email or password.");
    }

    [Fact]
    public async Task LoginAsync_WhenUserIsInactive_ThrowsUnauthorizedAccessException()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var email = "inactive@example.com";
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            FirstName = "Inactive",
            LastName = "User",
            Email = email,
            NormalizedEmail = email.ToUpperInvariant(),
            OrganizationId = orgId,
            IsActive = false
        };

        await _dbContext.ApplicationUsers.AddAsync(user);
        await _dbContext.SaveChangesAsync();

        var request = new LoginRequest
        {
            Email = email,
            Password = "Password123!",
            OrganizationId = orgId
        };

        var service = CreateService();

        // Act
        var act = () => service.LoginAsync(request);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("Your account is currently inactive.");
    }

    [Fact]
    public async Task LoginAsync_WhenOrganizationIsInactive_ThrowsBadRequestException()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var email = "active@example.com";
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            FirstName = "Active",
            LastName = "User",
            Email = email,
            NormalizedEmail = email.ToUpperInvariant(),
            OrganizationId = orgId,
            IsActive = true
        };

        await _dbContext.ApplicationUsers.AddAsync(user);
        await _dbContext.SaveChangesAsync();

        _organizationValidatorMock
            .Setup(v => v.ValidateOrganizationActiveAsync(orgId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new BadRequestException("Organization is currently inactive."));

        var request = new LoginRequest
        {
            Email = email,
            Password = "Password123!",
            OrganizationId = orgId
        };

        var service = CreateService();

        // Act
        var act = () => service.LoginAsync(request);

        // Assert
        await act.Should().ThrowAsync<BadRequestException>()
            .WithMessage("Organization is currently inactive.");
    }

    [Fact]
    public async Task LoginAsync_WhenPasswordIsIncorrect_ThrowsUnauthorizedAccessException()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var email = "valid@example.com";
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            FirstName = "Valid",
            LastName = "User",
            Email = email,
            NormalizedEmail = email.ToUpperInvariant(),
            OrganizationId = orgId,
            IsActive = true
        };

        await _dbContext.ApplicationUsers.AddAsync(user);
        await _dbContext.SaveChangesAsync();

        _userManagerMock.Setup(m => m.CheckPasswordAsync(user, "WrongPassword"))
            .ReturnsAsync(false);

        var request = new LoginRequest
        {
            Email = email,
            Password = "WrongPassword",
            OrganizationId = orgId
        };

        var service = CreateService();

        // Act
        var act = () => service.LoginAsync(request);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("Invalid email or password.");
    }

    [Fact]
    public async Task LoginAsync_WhenCredentialsValid_ReturnsAuthResponse()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var email = "student@example.com";
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            Email = email,
            NormalizedEmail = email.ToUpperInvariant(),
            UserName = email,
            FirstName = "Student",
            LastName = "One",
            OrganizationId = orgId,
            IsActive = true
        };

        await _dbContext.ApplicationUsers.AddAsync(user);
        await _dbContext.SaveChangesAsync();

        _userManagerMock.Setup(m => m.CheckPasswordAsync(user, "CorrectPassword123!"))
            .ReturnsAsync(true);

        _userManagerMock.Setup(m => m.GetRolesAsync(user))
            .ReturnsAsync(new List<string> { AppRoles.Student });

        _jwtTokenGeneratorMock.Setup(g => g.GenerateToken(user, It.IsAny<IEnumerable<string>>(), null))
            .Returns("login-jwt-token");

        _jwtTokenGeneratorMock.Setup(g => g.GenerateRefreshToken())
            .Returns("login-refresh-token");

        _userManagerMock.Setup(m => m.UpdateAsync(user))
            .ReturnsAsync(IdentityResult.Success);

        var request = new LoginRequest
        {
            Email = email,
            Password = "CorrectPassword123!",
            OrganizationId = orgId
        };

        var service = CreateService();

        // Act
        var response = await service.LoginAsync(request);

        // Assert
        response.Should().NotBeNull();
        response.Email.Should().Be(email);
        response.AccessToken.Should().Be("login-jwt-token");
        response.RefreshToken.Should().Be("login-refresh-token");
        response.Roles.Should().Contain(AppRoles.Student);

        user.RefreshToken.Should().Be("login-refresh-token");
        user.RefreshTokenExpiryTime.Should().BeAfter(DateTime.UtcNow);
    }

    #endregion

    #region RefreshTokenAsync Tests

    [Fact]
    public async Task RefreshTokenAsync_WhenUserNotFound_ThrowsUnauthorizedAccessException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _userManagerMock.Setup(m => m.FindByIdAsync(userId.ToString()))
            .ReturnsAsync((ApplicationUser?)null);

        var service = CreateService();
        var request = new RefreshTokenRequest { UserId = userId, RefreshToken = "any-token" };

        // Act
        var act = () => service.RefreshTokenAsync(request);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("Invalid refresh token.");
    }

    [Fact]
    public async Task RefreshTokenAsync_WhenTokenMismatches_ThrowsUnauthorizedAccessException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new ApplicationUser
        {
            Id = userId,
            RefreshToken = "real-token",
            RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(1),
            IsActive = true
        };

        _userManagerMock.Setup(m => m.FindByIdAsync(userId.ToString()))
            .ReturnsAsync(user);

        var service = CreateService();
        var request = new RefreshTokenRequest { UserId = userId, RefreshToken = "wrong-token" };

        // Act
        var act = () => service.RefreshTokenAsync(request);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("Invalid refresh token.");
    }

    [Fact]
    public async Task RefreshTokenAsync_WhenTokenExpired_ThrowsUnauthorizedAccessException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var token = "expired-token";
        var user = new ApplicationUser
        {
            Id = userId,
            RefreshToken = token,
            RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(-1), // Expired
            IsActive = true
        };

        _userManagerMock.Setup(m => m.FindByIdAsync(userId.ToString()))
            .ReturnsAsync(user);

        var service = CreateService();
        var request = new RefreshTokenRequest { UserId = userId, RefreshToken = token };

        // Act
        var act = () => service.RefreshTokenAsync(request);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*expired*");
    }

    [Fact]
    public async Task RefreshTokenAsync_WhenUserIsInactive_ThrowsUnauthorizedAccessException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var token = "valid-token";
        var user = new ApplicationUser
        {
            Id = userId,
            RefreshToken = token,
            RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(1),
            IsActive = false
        };

        _userManagerMock.Setup(m => m.FindByIdAsync(userId.ToString()))
            .ReturnsAsync(user);

        var service = CreateService();
        var request = new RefreshTokenRequest { UserId = userId, RefreshToken = token };

        // Act
        var act = () => service.RefreshTokenAsync(request);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("Your account is currently inactive.");
    }

    [Fact]
    public async Task RefreshTokenAsync_WhenValid_RotatesTokenAndReturnsAuthResponse()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var oldToken = "current-refresh-token";
        var user = new ApplicationUser
        {
            Id = userId,
            Email = "user@test.com",
            UserName = "user@test.com",
            RefreshToken = oldToken,
            RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(1),
            OrganizationId = Guid.NewGuid(),
            IsActive = true
        };

        _userManagerMock.Setup(m => m.FindByIdAsync(userId.ToString()))
            .ReturnsAsync(user);

        _userManagerMock.Setup(m => m.GetRolesAsync(user))
            .ReturnsAsync(new List<string> { AppRoles.Instructor });

        _jwtTokenGeneratorMock.Setup(g => g.GenerateToken(user, It.IsAny<IEnumerable<string>>(), null))
            .Returns("new-jwt-token");

        _jwtTokenGeneratorMock.Setup(g => g.GenerateRefreshToken())
            .Returns("new-refresh-token");

        _userManagerMock.Setup(m => m.UpdateAsync(user))
            .ReturnsAsync(IdentityResult.Success);

        var service = CreateService();
        var request = new RefreshTokenRequest { UserId = userId, RefreshToken = oldToken };

        // Act
        var response = await service.RefreshTokenAsync(request);

        // Assert
        response.Should().NotBeNull();
        response.AccessToken.Should().Be("new-jwt-token");
        response.RefreshToken.Should().Be("new-refresh-token");
        response.Roles.Should().Contain(AppRoles.Instructor);

        user.RefreshToken.Should().Be("new-refresh-token");
        _userManagerMock.Verify(m => m.UpdateAsync(user), Times.Once);
    }

    #endregion

    #region LogoutAsync Tests

    [Fact]
    public async Task LogoutAsync_WhenUserNotFound_CompletesSilently()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _userManagerMock.Setup(m => m.FindByIdAsync(userId.ToString()))
            .ReturnsAsync((ApplicationUser?)null);

        var service = CreateService();
        var request = new LogoutRequest { UserId = userId, RefreshToken = "some-token" };

        // Act
        var act = () => service.LogoutAsync(request);

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task LogoutAsync_WhenRefreshTokenMismatched_ThrowsUnauthorizedAccessException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new ApplicationUser
        {
            Id = userId,
            RefreshToken = "stored-token"
        };

        _userManagerMock.Setup(m => m.FindByIdAsync(userId.ToString()))
            .ReturnsAsync(user);

        var service = CreateService();
        var request = new LogoutRequest { UserId = userId, RefreshToken = "different-token" };

        // Act
        var act = () => service.LogoutAsync(request);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("Invalid refresh token.");
    }

    [Fact]
    public async Task LogoutAsync_WhenTokenMatches_RevokesTokenSuccessfully()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var token = "matching-token";
        var user = new ApplicationUser
        {
            Id = userId,
            RefreshToken = token,
            RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(2)
        };

        _userManagerMock.Setup(m => m.FindByIdAsync(userId.ToString()))
            .ReturnsAsync(user);

        _userManagerMock.Setup(m => m.UpdateAsync(user))
            .ReturnsAsync(IdentityResult.Success);

        var service = CreateService();
        var request = new LogoutRequest { UserId = userId, RefreshToken = token };

        // Act
        await service.LogoutAsync(request);

        // Assert
        user.RefreshToken.Should().BeNull();
        user.RefreshTokenExpiryTime.Should().BeOnOrBefore(DateTime.UtcNow);
        _userManagerMock.Verify(m => m.UpdateAsync(user), Times.Once);
    }

    #endregion
}
