using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using TrainHub.Application.Common.Security;
using TrainHub.Domain.Constants;
using TrainHub.Domain.Models;
using TrainHub.Infrastructure.Services;
using TrainHub.UnitTests.Common;

namespace TrainHub.UnitTests.Authentication;

public class JwtTokenGeneratorTests
{
    private readonly JwtSettings _jwtSettings;
    private readonly Mock<ILogger<JwtTokenGenerator>> _loggerMock;

    public JwtTokenGeneratorTests()
    {
        _jwtSettings = new JwtSettings
        {
            Secret = "super-secret-key-that-is-at-least-32-chars-long-12345",
            Issuer = "TrainHubIssuer",
            Audience = "TrainHubAudience",
            ExpiryMinutes = 60,
            RefreshTokenExpiryDays = 7
        };
        _loggerMock = new Mock<ILogger<JwtTokenGenerator>>();
    }

    [Fact]
    public void GenerateToken_WithValidUserAndRoles_ShouldReturnValidJwtWithExpectedClaims()
    {
        // Arrange
        var userManagerMock = TestHelpers.CreateMockUserManager();
        var generator = new JwtTokenGenerator(
            Options.Create(_jwtSettings),
            userManagerMock.Object,
            _loggerMock.Object);

        var userId = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        var user = new ApplicationUser
        {
            Id = userId,
            Email = "student@test.com",
            UserName = "student@test.com",
            FirstName = "John",
            LastName = "Doe",
            OrganizationId = orgId
        };
        var roles = new[] { AppRoles.Student };

        // Act
        var tokenString = generator.GenerateToken(user, roles);

        // Assert
        tokenString.Should().NotBeNullOrWhiteSpace();

        var handler = new JwtSecurityTokenHandler();
        var token = handler.ReadJwtToken(tokenString);

        token.Issuer.Should().Be(_jwtSettings.Issuer);
        token.Audiences.Should().Contain(_jwtSettings.Audience);

        var claims = token.Claims.ToList();
        claims.Should().Contain(c => c.Type == "sub" && c.Value == userId.ToString());
        claims.Should().Contain(c => c.Type == "nameid" && c.Value == userId.ToString());
        claims.Should().Contain(c => c.Type == "email" && c.Value == "student@test.com");
        claims.Should().Contain(c => c.Type == "given_name" && c.Value == "John");
        claims.Should().Contain(c => c.Type == "family_name" && c.Value == "Doe");
        claims.Should().Contain(c => c.Type == "organizationId" && c.Value == orgId.ToString());
        claims.Should().Contain(c => c.Type == "role" && c.Value == AppRoles.Student);
    }

    [Fact]
    public async Task GenerateTokenAsync_ShouldRetrieveRolesFromUserManagerAndReturnToken()
    {
        // Arrange
        var userManagerMock = TestHelpers.CreateMockUserManager();
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            Email = "admin@test.com",
            UserName = "admin@test.com",
            FirstName = "Admin",
            LastName = "User",
            OrganizationId = Guid.NewGuid()
        };

        userManagerMock.Setup(m => m.GetRolesAsync(user))
            .ReturnsAsync(new List<string> { AppRoles.OrgAdmin });
        userManagerMock.Setup(m => m.GetClaimsAsync(user))
            .ReturnsAsync(new List<Claim>());

        var generator = new JwtTokenGenerator(
            Options.Create(_jwtSettings),
            userManagerMock.Object,
            _loggerMock.Object);

        // Act
        var tokenString = await generator.GenerateTokenAsync(user);

        // Assert
        tokenString.Should().NotBeNullOrWhiteSpace();

        var handler = new JwtSecurityTokenHandler();
        var token = handler.ReadJwtToken(tokenString);
        token.Claims.Should().Contain(c => c.Type == "role" && c.Value == AppRoles.OrgAdmin);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void GenerateToken_WithMissingSecret_ShouldThrowInvalidOperationException(string? secret)
    {
        // Arrange
        var settings = new JwtSettings { Secret = secret! };
        var userManagerMock = TestHelpers.CreateMockUserManager();
        var generator = new JwtTokenGenerator(
            Options.Create(settings),
            userManagerMock.Object,
            _loggerMock.Object);

        var user = new ApplicationUser { Id = Guid.NewGuid() };

        // Act
        var act = () => generator.GenerateToken(user, new[] { AppRoles.Student });

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*JWT Secret key is missing or not configured*");
    }

    [Fact]
    public void GenerateToken_WithShortSecret_ShouldThrowInvalidOperationException()
    {
        // Arrange (secret with fewer than 32 bytes)
        var settings = new JwtSettings { Secret = "short-secret-key-123" };
        var userManagerMock = TestHelpers.CreateMockUserManager();
        var generator = new JwtTokenGenerator(
            Options.Create(settings),
            userManagerMock.Object,
            _loggerMock.Object);

        var user = new ApplicationUser { Id = Guid.NewGuid() };

        // Act
        var act = () => generator.GenerateToken(user, new[] { AppRoles.Student });

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*JWT Secret key must be at least 32 characters*");
    }

    [Fact]
    public void GenerateRefreshToken_ShouldReturnValidBase64String()
    {
        // Arrange
        var userManagerMock = TestHelpers.CreateMockUserManager();
        var generator = new JwtTokenGenerator(
            Options.Create(_jwtSettings),
            userManagerMock.Object,
            _loggerMock.Object);

        // Act
        var refreshToken = generator.GenerateRefreshToken();

        // Assert
        refreshToken.Should().NotBeNullOrWhiteSpace();
        var bytes = Convert.FromBase64String(refreshToken);
        bytes.Length.Should().Be(64);
    }
}
