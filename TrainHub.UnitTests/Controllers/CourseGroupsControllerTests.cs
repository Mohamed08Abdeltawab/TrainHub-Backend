using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using TrainHub.Api.Controllers;
using TrainHub.Application.Common.Models;
using TrainHub.Application.Features.CourseGroups.DTOs;
using TrainHub.Application.Interfaces;

namespace TrainHub.UnitTests.Controllers;

public class CourseGroupsControllerTests
{
    private readonly Mock<ICourseGroupService> _courseGroupServiceMock;
    private readonly Mock<ILogger<CourseGroupsController>> _loggerMock;
    private readonly CourseGroupsController _controller;

    public CourseGroupsControllerTests()
    {
        _courseGroupServiceMock = new Mock<ICourseGroupService>();
        _loggerMock = new Mock<ILogger<CourseGroupsController>>();
        _controller = new CourseGroupsController(_courseGroupServiceMock.Object, _loggerMock.Object);
    }

    [Fact]
    public async Task GetAll_ReturnsOkResultWithPagedResult()
    {
        // Arrange
        var pagedResult = new PagedResult<CourseGroupDto>(
            new List<CourseGroupDto>
            {
                new() { Id = Guid.NewGuid(), Name = "Group 1" },
                new() { Id = Guid.NewGuid(), Name = "Group 2" }
            },
            totalCount: 2,
            pageNumber: 1,
            pageSize: 10);

        _courseGroupServiceMock.Setup(s => s.GetPagedAsync(It.IsAny<CourseGroupQueryParameters>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(pagedResult);

        // Act
        var result = await _controller.GetAll(new CourseGroupQueryParameters(), CancellationToken.None);

        // Assert
        var okResult = result.Result as OkObjectResult;
        okResult.Should().NotBeNull();
        okResult!.StatusCode.Should().Be(200);
        okResult.Value.Should().BeEquivalentTo(pagedResult);
    }

    [Fact]
    public async Task GetById_ReturnsOkResultWithCourseGroup()
    {
        // Arrange
        var groupId = Guid.NewGuid();
        var expectedGroup = new CourseGroupDto
        {
            Id = groupId,
            Name = "Group 1",
            CourseName = "ASP.NET Core",
            StartDate = new DateOnly(2026, 10, 1),
            EndDate = new DateOnly(2026, 12, 1)
        };

        _courseGroupServiceMock.Setup(s => s.GetByIdAsync(groupId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedGroup);

        // Act
        var result = await _controller.GetById(groupId, CancellationToken.None);

        // Assert
        var okResult = result.Result as OkObjectResult;
        okResult.Should().NotBeNull();
        okResult!.StatusCode.Should().Be(200);
        okResult.Value.Should().BeEquivalentTo(expectedGroup);
    }

    [Fact]
    public async Task Create_ReturnsCreatedAtActionResult()
    {
        // Arrange
        var dto = new CreateCourseGroupDto
        {
            CourseId = Guid.NewGuid(),
            Name = "New Group",
            StartDate = new DateOnly(2026, 10, 1),
            EndDate = new DateOnly(2026, 12, 1)
        };

        var createdGroup = new CourseGroupDto
        {
            Id = Guid.NewGuid(),
            Name = dto.Name,
            StartDate = dto.StartDate,
            EndDate = dto.EndDate
        };

        _courseGroupServiceMock.Setup(s => s.CreateAsync(dto, It.IsAny<CancellationToken>()))
            .ReturnsAsync(createdGroup);

        // Act
        var result = await _controller.Create(dto, CancellationToken.None);

        // Assert
        var createdResult = result.Result as CreatedAtActionResult;
        createdResult.Should().NotBeNull();
        createdResult!.StatusCode.Should().Be(201);
        createdResult.ActionName.Should().Be(nameof(CourseGroupsController.GetById));
        createdResult.RouteValues!["id"].Should().Be(createdGroup.Id);
        createdResult.Value.Should().BeEquivalentTo(createdGroup);
    }

    [Fact]
    public async Task Update_ReturnsOkResultWithUpdatedGroup()
    {
        // Arrange
        var groupId = Guid.NewGuid();
        var dto = new UpdateCourseGroupDto
        {
            Name = "Updated Group",
            StartDate = new DateOnly(2026, 10, 1),
            EndDate = new DateOnly(2026, 12, 1)
        };

        var updatedGroup = new CourseGroupDto
        {
            Id = groupId,
            Name = dto.Name,
            StartDate = dto.StartDate,
            EndDate = dto.EndDate
        };

        _courseGroupServiceMock.Setup(s => s.UpdateAsync(groupId, dto, It.IsAny<CancellationToken>()))
            .ReturnsAsync(updatedGroup);

        // Act
        var result = await _controller.Update(groupId, dto, CancellationToken.None);

        // Assert
        var okResult = result.Result as OkObjectResult;
        okResult.Should().NotBeNull();
        okResult!.StatusCode.Should().Be(200);
        okResult.Value.Should().BeEquivalentTo(updatedGroup);
    }

    [Fact]
    public async Task Delete_ReturnsNoContentResult()
    {
        // Arrange
        var groupId = Guid.NewGuid();
        _courseGroupServiceMock.Setup(s => s.DeleteAsync(groupId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _controller.Delete(groupId, CancellationToken.None);

        // Assert
        var noContentResult = result as NoContentResult;
        noContentResult.Should().NotBeNull();
        noContentResult!.StatusCode.Should().Be(204);
    }
}
