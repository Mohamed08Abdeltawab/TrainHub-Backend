using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using TrainHub.Api.Controllers;
using TrainHub.Application.Features.Instructors.DTOs;
using TrainHub.Application.Interfaces;

namespace TrainHub.UnitTests.Controllers;

public class InstructorsControllerTests
{
    private readonly Mock<IInstructorService> _instructorServiceMock;
    private readonly Mock<ILogger<InstructorsController>> _loggerMock;
    private readonly InstructorsController _controller;

    public InstructorsControllerTests()
    {
        _instructorServiceMock = new Mock<IInstructorService>();
        _loggerMock = new Mock<ILogger<InstructorsController>>();
        _controller = new InstructorsController(
            _instructorServiceMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task GetAll_ReturnsOkResultWithPagedResult()
    {
        // Arrange
        var pagedResult = new TrainHub.Application.Common.Models.PagedResult<InstructorDto>(
            new List<InstructorDto>
            {
                new() { Id = Guid.NewGuid(), Specialization = "DevOps" },
                new() { Id = Guid.NewGuid(), Specialization = "Cloud" }
            },
            totalCount: 2,
            pageNumber: 1,
            pageSize: 10);

        _instructorServiceMock.Setup(s => s.GetPagedAsync(It.IsAny<InstructorQueryParameters>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(pagedResult);

        // Act
        var result = await _controller.GetAll(new InstructorQueryParameters(), CancellationToken.None);

        // Assert
        var okResult = result.Result as OkObjectResult;
        okResult.Should().NotBeNull();
        okResult!.StatusCode.Should().Be(200);
        okResult.Value.Should().BeEquivalentTo(pagedResult);
    }

    [Fact]
    public async Task GetById_ReturnsOkResultWithInstructor()
    {
        // Arrange
        var instructorId = Guid.NewGuid();
        var expectedInstructor = new InstructorDto
        {
            Id = instructorId,
            Specialization = "Machine Learning",
            UserId = Guid.NewGuid(),
            OrganizationId = Guid.NewGuid()
        };

        _instructorServiceMock.Setup(s => s.GetByIdAsync(instructorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedInstructor);

        // Act
        var result = await _controller.GetById(instructorId, CancellationToken.None);

        // Assert
        var okResult = result.Result as OkObjectResult;
        okResult.Should().NotBeNull();
        okResult!.StatusCode.Should().Be(200);
        okResult.Value.Should().BeEquivalentTo(expectedInstructor);
    }

    [Fact]
    public async Task Create_ReturnsCreatedAtActionResult()
    {
        // Arrange
        var createDto = new CreateInstructorDto
        {
            UserId = Guid.NewGuid(),
            Specialization = "Cybersecurity",
            Bio = "Security specialist"
        };

        var createdInstructor = new InstructorDto
        {
            Id = Guid.NewGuid(),
            UserId = createDto.UserId,
            Specialization = createDto.Specialization,
            Bio = createDto.Bio
        };

        _instructorServiceMock.Setup(s => s.CreateAsync(createDto, It.IsAny<CancellationToken>()))
            .ReturnsAsync(createdInstructor);

        // Act
        var result = await _controller.Create(createDto, CancellationToken.None);

        // Assert
        var createdAtActionResult = result.Result as CreatedAtActionResult;
        createdAtActionResult.Should().NotBeNull();
        createdAtActionResult!.StatusCode.Should().Be(201);
        createdAtActionResult.ActionName.Should().Be(nameof(InstructorsController.GetById));
        createdAtActionResult.RouteValues.Should().ContainKey("id");
        createdAtActionResult.RouteValues!["id"].Should().Be(createdInstructor.Id);
        createdAtActionResult.Value.Should().BeEquivalentTo(createdInstructor);
    }

    [Fact]
    public async Task Update_WhenCalled_ReturnsOkResultWithUpdatedInstructor()
    {
        // Arrange
        var instructorId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var updateDto = new UpdateInstructorDto
        {
            Specialization = "Updated Specialization",
            Bio = "Updated bio"
        };

        var updatedInstructor = new InstructorDto
        {
            Id = instructorId,
            UserId = userId,
            Specialization = updateDto.Specialization,
            Bio = updateDto.Bio
        };

        _instructorServiceMock.Setup(s => s.UpdateAsync(instructorId, updateDto, It.IsAny<CancellationToken>()))
            .ReturnsAsync(updatedInstructor);

        // Act
        var result = await _controller.Update(instructorId, updateDto, CancellationToken.None);

        // Assert
        var okResult = result.Result as OkObjectResult;
        okResult.Should().NotBeNull();
        okResult!.StatusCode.Should().Be(200);
        okResult.Value.Should().BeEquivalentTo(updatedInstructor);

        _instructorServiceMock.Verify(s => s.UpdateAsync(instructorId, updateDto, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Delete_ReturnsNoContentResult()
    {
        // Arrange
        var instructorId = Guid.NewGuid();

        _instructorServiceMock.Setup(s => s.DeleteAsync(instructorId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _controller.Delete(instructorId, CancellationToken.None);

        // Assert
        var noContentResult = result as NoContentResult;
        noContentResult.Should().NotBeNull();
        noContentResult!.StatusCode.Should().Be(204);

        _instructorServiceMock.Verify(s => s.DeleteAsync(instructorId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task LinkUser_ReturnsOkResultWithUpdatedInstructor()
    {
        // Arrange
        var instructorId = Guid.NewGuid();
        var linkDto = new LinkInstructorUserDto { UserId = Guid.NewGuid() };
        var linkedInstructor = new InstructorDto
        {
            Id = instructorId,
            UserId = linkDto.UserId,
            Specialization = "Distributed Systems"
        };

        _instructorServiceMock.Setup(s => s.LinkUserAsync(instructorId, linkDto, It.IsAny<CancellationToken>()))
            .ReturnsAsync(linkedInstructor);

        // Act
        var result = await _controller.LinkUser(instructorId, linkDto, CancellationToken.None);

        // Assert
        var okResult = result.Result as OkObjectResult;
        okResult.Should().NotBeNull();
        okResult!.StatusCode.Should().Be(200);
        okResult.Value.Should().BeEquivalentTo(linkedInstructor);
    }

    [Fact]
    public async Task GetByUserId_ReturnsOkResultWithInstructor()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var instructor = new InstructorDto
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Specialization = "Networks & Security"
        };

        _instructorServiceMock.Setup(s => s.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(instructor);

        // Act
        var result = await _controller.GetByUserId(userId, CancellationToken.None);

        // Assert
        var okResult = result.Result as OkObjectResult;
        okResult.Should().NotBeNull();
        okResult!.StatusCode.Should().Be(200);
        okResult.Value.Should().BeEquivalentTo(instructor);
    }

    [Fact]
    public async Task GetCurrentInstructor_WithValidUserClaim_ReturnsOkResult()
    {
        // Arrange
        var currentUserId = Guid.NewGuid();
        var claims = new[] { new Claim(ClaimTypes.NameIdentifier, currentUserId.ToString()) };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
        };

        var expectedInstructor = new InstructorDto
        {
            Id = Guid.NewGuid(),
            UserId = currentUserId,
            Specialization = "Web Development"
        };

        _instructorServiceMock.Setup(s => s.GetCurrentInstructorAsync(currentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedInstructor);

        // Act
        var result = await _controller.GetCurrentInstructor(CancellationToken.None);

        // Assert
        var okResult = result.Result as OkObjectResult;
        okResult.Should().NotBeNull();
        okResult!.StatusCode.Should().Be(200);
        okResult.Value.Should().BeEquivalentTo(expectedInstructor);
    }

    [Fact]
    public async Task GetCurrentInstructor_WithoutUserClaim_ReturnsUnauthorized()
    {
        // Arrange
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity()) }
        };

        // Act
        var result = await _controller.GetCurrentInstructor(CancellationToken.None);

        // Assert
        var unauthorizedResult = result.Result as UnauthorizedResult;
        unauthorizedResult.Should().NotBeNull();
        unauthorizedResult!.StatusCode.Should().Be(401);
    }

    [Fact]
    public async Task UpdateCurrentInstructor_WithValidUserClaim_ReturnsOkResult()
    {
        // Arrange
        var currentUserId = Guid.NewGuid();
        var claims = new[] { new Claim(ClaimTypes.NameIdentifier, currentUserId.ToString()) };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
        };

        var updateDto = new UpdateInstructorProfileDto { Bio = "Updated personal bio", PhoneNumber = "+1234567890" };
        var updatedInstructor = new InstructorDto
        {
            Id = Guid.NewGuid(),
            UserId = currentUserId,
            Bio = updateDto.Bio,
            PhoneNumber = updateDto.PhoneNumber
        };

        _instructorServiceMock.Setup(s => s.UpdateCurrentInstructorProfileAsync(currentUserId, updateDto, It.IsAny<CancellationToken>()))
            .ReturnsAsync(updatedInstructor);

        // Act
        var result = await _controller.UpdateCurrentInstructor(updateDto, CancellationToken.None);

        // Assert
        var okResult = result.Result as OkObjectResult;
        okResult.Should().NotBeNull();
        okResult!.StatusCode.Should().Be(200);
        okResult.Value.Should().BeEquivalentTo(updatedInstructor);
    }

    [Fact]
    public async Task UpdateCurrentInstructor_WithoutUserClaim_ReturnsUnauthorized()
    {
        // Arrange
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity()) }
        };

        var updateDto = new UpdateInstructorProfileDto { Bio = "Updated personal bio" };

        // Act
        var result = await _controller.UpdateCurrentInstructor(updateDto, CancellationToken.None);

        // Assert
        var unauthorizedResult = result.Result as UnauthorizedResult;
        unauthorizedResult.Should().NotBeNull();
        unauthorizedResult!.StatusCode.Should().Be(401);
    }
}
