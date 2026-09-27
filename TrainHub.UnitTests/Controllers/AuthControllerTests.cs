using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using TrainHub.Api.Controllers;
using TrainHub.Application.Features.Auth.DTOs;
using TrainHub.Application.Interfaces;

namespace TrainHub.UnitTests.Controllers;

public class AuthControllerTests
{
    private readonly Mock<IAuthService> _authServiceMock;
    private readonly Mock<ILogger<AuthController>> _loggerMock;
    private readonly AuthController _controller;

    public AuthControllerTests()
    {
        _authServiceMock = new Mock<IAuthService>();
        _loggerMock = new Mock<ILogger<AuthController>>();
        _controller = new AuthController(_authServiceMock.Object, _loggerMock.Object);
    }

    [Fact]
    public async Task Register_WithValidRequest_ReturnsCreatedAtActionResult()
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

        var expectedResponse = new AuthResponse
        {
            UserId = Guid.NewGuid(),
            Email = request.Email,
            AccessToken = "token",
            RefreshToken = "refresh-token"
        };

        _authServiceMock.Setup(s => s.RegisterAsync(request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResponse);

        // Act
        var actionResult = await _controller.Register(request, CancellationToken.None);

        // Assert
        var createdResult = actionResult.Result as CreatedAtActionResult;
        createdResult.Should().NotBeNull();
        createdResult!.StatusCode.Should().Be(201);
        createdResult.Value.Should().BeEquivalentTo(expectedResponse);
    }

    [Fact]
    public async Task Login_WithValidCredentials_ReturnsOkResultWithAuthResponse()
    {
        // Arrange
        var request = new LoginRequest
        {
            Email = "jane@example.com",
            Password = "Password123!",
            OrganizationId = Guid.NewGuid()
        };

        var expectedResponse = new AuthResponse
        {
            UserId = Guid.NewGuid(),
            Email = request.Email,
            AccessToken = "access-token",
            RefreshToken = "refresh-token"
        };

        _authServiceMock.Setup(s => s.LoginAsync(request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResponse);

        // Act
        var actionResult = await _controller.Login(request, CancellationToken.None);

        // Assert
        var okResult = actionResult.Result as OkObjectResult;
        okResult.Should().NotBeNull();
        okResult!.StatusCode.Should().Be(200);
        okResult.Value.Should().BeEquivalentTo(expectedResponse);
    }

    [Fact]
    public async Task RefreshToken_WithValidToken_ReturnsOkResultWithNewTokens()
    {
        // Arrange
        var request = new RefreshTokenRequest
        {
            UserId = Guid.NewGuid(),
            RefreshToken = "valid-refresh-token"
        };

        var expectedResponse = new AuthResponse
        {
            UserId = request.UserId,
            Email = "user@example.com",
            AccessToken = "new-access-token",
            RefreshToken = "new-refresh-token"
        };

        _authServiceMock.Setup(s => s.RefreshTokenAsync(request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResponse);

        // Act
        var actionResult = await _controller.RefreshToken(request, CancellationToken.None);

        // Assert
        var okResult = actionResult.Result as OkObjectResult;
        okResult.Should().NotBeNull();
        okResult!.StatusCode.Should().Be(200);
        okResult.Value.Should().BeEquivalentTo(expectedResponse);
    }

    [Fact]
    public async Task Logout_WithValidRequest_ReturnsOkWithMessage()
    {
        // Arrange
        var request = new LogoutRequest
        {
            UserId = Guid.NewGuid(),
            RefreshToken = "token-to-revoke"
        };

        _authServiceMock.Setup(s => s.LogoutAsync(request, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var actionResult = await _controller.Logout(request, CancellationToken.None);

        // Assert
        var okResult = actionResult as OkObjectResult;
        okResult.Should().NotBeNull();
        okResult!.StatusCode.Should().Be(200);
        _authServiceMock.Verify(s => s.LogoutAsync(request, It.IsAny<CancellationToken>()), Times.Once);
    }
}
