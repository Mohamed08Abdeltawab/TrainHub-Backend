using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using TrainHub.Api.Controllers;
using TrainHub.Application.Common.Models;
using TrainHub.Application.Features.Attendances.DTOs;
using TrainHub.Application.Interfaces;

namespace TrainHub.UnitTests.Controllers;

public class AttendancesControllerTests
{
    private readonly Mock<IAttendanceService> _attendanceServiceMock;
    private readonly Mock<ILogger<AttendancesController>> _loggerMock;
    private readonly AttendancesController _controller;
    private readonly Guid _userId = Guid.NewGuid();

    public AttendancesControllerTests()
    {
        _attendanceServiceMock = new Mock<IAttendanceService>();
        _loggerMock = new Mock<ILogger<AttendancesController>>();
        _controller = new AttendancesController(_attendanceServiceMock.Object, _loggerMock.Object);

        // Setup ControllerContext with authenticated user claim
        var user = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, _userId.ToString())
        ], "TestAuth"));

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = user }
        };
    }

    [Fact]
    public async Task GetAll_ReturnsOkResultWithPagedResult()
    {
        // Arrange
        var pagedResult = new PagedResult<AttendanceDto>(
            new List<AttendanceDto>
            {
                new() { Id = Guid.NewGuid(), StudentName = "Alice", StatusName = "Present" },
                new() { Id = Guid.NewGuid(), StudentName = "Bob", StatusName = "Late" }
            },
            totalCount: 2,
            pageNumber: 1,
            pageSize: 10);

        _attendanceServiceMock.Setup(s => s.GetPagedAsync(It.IsAny<AttendanceQueryParameters>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(pagedResult);

        // Act
        var result = await _controller.GetAll(new AttendanceQueryParameters(), CancellationToken.None);

        // Assert
        var okResult = result.Result as OkObjectResult;
        okResult.Should().NotBeNull();
        okResult!.StatusCode.Should().Be(200);
        okResult.Value.Should().BeEquivalentTo(pagedResult);
    }

    [Fact]
    public async Task GetById_ReturnsOkResultWithAttendanceDto()
    {
        // Arrange
        var id = Guid.NewGuid();
        var dto = new AttendanceDto
        {
            Id = id,
            StudentName = "Alice",
            StatusName = "Present"
        };

        _attendanceServiceMock.Setup(s => s.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(dto);

        // Act
        var result = await _controller.GetById(id, CancellationToken.None);

        // Assert
        var okResult = result.Result as OkObjectResult;
        okResult.Should().NotBeNull();
        okResult!.StatusCode.Should().Be(200);
        okResult.Value.Should().BeEquivalentTo(dto);
    }

    [Fact]
    public async Task GetBySessionId_ReturnsOkResultWithList()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var dtos = new List<AttendanceDto>
        {
            new() { Id = Guid.NewGuid(), SessionId = sessionId, StudentName = "Alice" }
        };

        _attendanceServiceMock.Setup(s => s.GetBySessionIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(dtos);

        // Act
        var result = await _controller.GetBySessionId(sessionId, CancellationToken.None);

        // Assert
        var okResult = result.Result as OkObjectResult;
        okResult.Should().NotBeNull();
        okResult!.StatusCode.Should().Be(200);
        okResult.Value.Should().BeEquivalentTo(dtos);
    }

    [Fact]
    public async Task Create_ReturnsCreatedAtActionResult()
    {
        // Arrange
        var dto = new CreateAttendanceDto
        {
            SessionId = Guid.NewGuid(),
            StudentId = Guid.NewGuid(),
            Status = 1
        };

        var created = new AttendanceDto
        {
            Id = Guid.NewGuid(),
            SessionId = dto.SessionId,
            StudentId = dto.StudentId,
            Status = 1,
            StatusName = "Present"
        };

        _attendanceServiceMock.Setup(s => s.CreateAsync(dto, _userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(created);

        // Act
        var result = await _controller.Create(dto, CancellationToken.None);

        // Assert
        var createdResult = result.Result as CreatedAtActionResult;
        createdResult.Should().NotBeNull();
        createdResult!.StatusCode.Should().Be(201);
        createdResult.ActionName.Should().Be(nameof(AttendancesController.GetById));
        createdResult.RouteValues.Should().ContainKey("id").WhoseValue.Should().Be(created.Id);
        createdResult.Value.Should().BeEquivalentTo(created);
    }

    [Fact]
    public async Task Update_ReturnsOkResultWithUpdatedDto()
    {
        // Arrange
        var id = Guid.NewGuid();
        var updateDto = new UpdateAttendanceDto { Status = 2, Notes = "Updated notes" };
        var updated = new AttendanceDto { Id = id, Status = 2, StatusName = "Late" };

        _attendanceServiceMock.Setup(s => s.UpdateAsync(id, updateDto, _userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(updated);

        // Act
        var result = await _controller.Update(id, updateDto, CancellationToken.None);

        // Assert
        var okResult = result.Result as OkObjectResult;
        okResult.Should().NotBeNull();
        okResult!.StatusCode.Should().Be(200);
        okResult.Value.Should().BeEquivalentTo(updated);
    }

    [Fact]
    public async Task UpdateStatus_ReturnsOkResultWithUpdatedDto()
    {
        // Arrange
        var id = Guid.NewGuid();
        var statusDto = new UpdateAttendanceStatusDto { Status = 0 };
        var updated = new AttendanceDto { Id = id, Status = 0, StatusName = "Absent" };

        _attendanceServiceMock.Setup(s => s.UpdateStatusAsync(id, statusDto, _userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(updated);

        // Act
        var result = await _controller.UpdateStatus(id, statusDto, CancellationToken.None);

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
        var id = Guid.NewGuid();
        _attendanceServiceMock.Setup(s => s.DeleteAsync(id, _userId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _controller.Delete(id, CancellationToken.None);

        // Assert
        var noContentResult = result as NoContentResult;
        noContentResult.Should().NotBeNull();
        noContentResult!.StatusCode.Should().Be(204);
    }
}
