using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using TrainHub.Api.Controllers;
using TrainHub.Application.Common.Models;
using TrainHub.Application.Features.Sessions.DTOs;
using TrainHub.Application.Interfaces;

namespace TrainHub.UnitTests.Controllers;

public class SessionsControllerTests
{
    private readonly Mock<ISessionService> _sessionServiceMock;
    private readonly Mock<ILogger<SessionsController>> _loggerMock;
    private readonly SessionsController _controller;

    public SessionsControllerTests()
    {
        _sessionServiceMock = new Mock<ISessionService>();
        _loggerMock = new Mock<ILogger<SessionsController>>();
        _controller = new SessionsController(_sessionServiceMock.Object, _loggerMock.Object);
    }

    [Fact]
    public async Task GetAll_ReturnsOkResultWithPagedResult()
    {
        // Arrange
        var pagedResult = new PagedResult<SessionDto>(
            new List<SessionDto>
            {
                new() { Id = Guid.NewGuid(), CourseGroupName = "Cohort 1", Room = "Room 101" },
                new() { Id = Guid.NewGuid(), CourseGroupName = "Cohort 2", Room = "Room 102" }
            },
            totalCount: 2,
            pageNumber: 1,
            pageSize: 10);

        _sessionServiceMock.Setup(s => s.GetPagedAsync(It.IsAny<SessionQueryParameters>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(pagedResult);

        // Act
        var result = await _controller.GetAll(new SessionQueryParameters(), CancellationToken.None);

        // Assert
        var okResult = result.Result as OkObjectResult;
        okResult.Should().NotBeNull();
        okResult!.StatusCode.Should().Be(200);
        okResult.Value.Should().BeEquivalentTo(pagedResult);
    }

    [Fact]
    public async Task GetById_ReturnsOkResultWithSession()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var expected = new SessionDto
        {
            Id = sessionId,
            CourseGroupName = "Cohort 1",
            Room = "Lab A"
        };

        _sessionServiceMock.Setup(s => s.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        // Act
        var result = await _controller.GetById(sessionId, CancellationToken.None);

        // Assert
        var okResult = result.Result as OkObjectResult;
        okResult.Should().NotBeNull();
        okResult!.StatusCode.Should().Be(200);
        okResult.Value.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public async Task GetByCourseGroupId_ReturnsOkResultWithList()
    {
        // Arrange
        var courseGroupId = Guid.NewGuid();
        var list = new List<SessionDto>
        {
            new() { Id = Guid.NewGuid(), CourseGroupId = courseGroupId, Room = "Room 1" },
            new() { Id = Guid.NewGuid(), CourseGroupId = courseGroupId, Room = "Room 2" }
        };

        _sessionServiceMock.Setup(s => s.GetByCourseGroupIdAsync(courseGroupId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(list);

        // Act
        var result = await _controller.GetByCourseGroupId(courseGroupId, CancellationToken.None);

        // Assert
        var okResult = result.Result as OkObjectResult;
        okResult.Should().NotBeNull();
        okResult!.StatusCode.Should().Be(200);
        okResult.Value.Should().BeEquivalentTo(list);
    }

    [Fact]
    public async Task Create_ReturnsCreatedAtActionResult()
    {
        // Arrange
        var dto = new CreateSessionDto
        {
            CourseGroupId = Guid.NewGuid(),
            StartTime = DateTime.UtcNow.AddDays(1),
            EndTime = DateTime.UtcNow.AddDays(1).AddHours(2)
        };
        var created = new SessionDto
        {
            Id = Guid.NewGuid(),
            CourseGroupId = dto.CourseGroupId,
            StartTime = dto.StartTime,
            EndTime = dto.EndTime
        };

        _sessionServiceMock.Setup(s => s.CreateAsync(dto, It.IsAny<CancellationToken>()))
            .ReturnsAsync(created);

        // Act
        var result = await _controller.Create(dto, CancellationToken.None);

        // Assert
        var createdResult = result.Result as CreatedAtActionResult;
        createdResult.Should().NotBeNull();
        createdResult!.StatusCode.Should().Be(201);
        createdResult.ActionName.Should().Be(nameof(SessionsController.GetById));
        createdResult.RouteValues!["id"].Should().Be(created.Id);
        createdResult.Value.Should().BeEquivalentTo(created);
    }

    [Fact]
    public async Task Update_ReturnsOkResult()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var dto = new UpdateSessionDto
        {
            StartTime = DateTime.UtcNow.AddDays(2),
            EndTime = DateTime.UtcNow.AddDays(2).AddHours(2)
        };
        var updated = new SessionDto
        {
            Id = sessionId,
            StartTime = dto.StartTime,
            EndTime = dto.EndTime
        };

        _sessionServiceMock.Setup(s => s.UpdateAsync(sessionId, dto, It.IsAny<CancellationToken>()))
            .ReturnsAsync(updated);

        // Act
        var result = await _controller.Update(sessionId, dto, CancellationToken.None);

        // Assert
        var okResult = result.Result as OkObjectResult;
        okResult.Should().NotBeNull();
        okResult!.StatusCode.Should().Be(200);
        okResult.Value.Should().BeEquivalentTo(updated);
    }

    [Fact]
    public async Task UpdateStatus_ReturnsOkResult()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var dto = new UpdateSessionStatusDto { Status = 2 };
        var updated = new SessionDto { Id = sessionId, Status = 2 };

        _sessionServiceMock.Setup(s => s.UpdateStatusAsync(sessionId, dto, It.IsAny<CancellationToken>()))
            .ReturnsAsync(updated);

        // Act
        var result = await _controller.UpdateStatus(sessionId, dto, CancellationToken.None);

        // Assert
        var okResult = result.Result as OkObjectResult;
        okResult.Should().NotBeNull();
        okResult!.StatusCode.Should().Be(200);
        okResult.Value.Should().BeEquivalentTo(updated);
    }

    [Fact]
    public async Task Delete_ReturnsNoContentResult()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        _sessionServiceMock.Setup(s => s.DeleteAsync(sessionId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _controller.Delete(sessionId, CancellationToken.None);

        // Assert
        var noContentResult = result as NoContentResult;
        noContentResult.Should().NotBeNull();
        noContentResult!.StatusCode.Should().Be(204);
    }
}
