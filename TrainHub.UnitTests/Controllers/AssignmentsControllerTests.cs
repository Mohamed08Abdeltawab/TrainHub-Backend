using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using TrainHub.Api.Controllers;
using TrainHub.Application.Common.Models;
using TrainHub.Application.Features.Assignments.DTOs;
using TrainHub.Application.Interfaces;
using TrainHub.Domain.Constants;

namespace TrainHub.UnitTests.Controllers;

public class AssignmentsControllerTests
{
    private readonly Mock<IAssignmentService> _assignmentServiceMock;
    private readonly Mock<ILogger<AssignmentsController>> _loggerMock;
    private readonly AssignmentsController _controller;
    private readonly Guid _userId = Guid.NewGuid();

    public AssignmentsControllerTests()
    {
        _assignmentServiceMock = new Mock<IAssignmentService>();
        _loggerMock = new Mock<ILogger<AssignmentsController>>();
        _controller = new AssignmentsController(_assignmentServiceMock.Object, _loggerMock.Object);

        var user = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, _userId.ToString()),
            new Claim(ClaimTypes.Role, AppRoles.Instructor)
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
        var pagedResult = new PagedResult<AssignmentDto>(
            new List<AssignmentDto>
            {
                new() { Id = Guid.NewGuid(), Title = "Assignment 1", CourseGroupName = "Cohort A", DueDate = DateTime.UtcNow.AddDays(7) },
                new() { Id = Guid.NewGuid(), Title = "Assignment 2", CourseGroupName = "Cohort A", DueDate = DateTime.UtcNow.AddDays(14) }
            },
            totalCount: 2,
            pageNumber: 1,
            pageSize: 10);

        _assignmentServiceMock.Setup(s => s.GetPagedAsync(It.IsAny<AssignmentQueryParameters>(), _userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pagedResult);

        // Act
        var result = await _controller.GetAll(new AssignmentQueryParameters(), CancellationToken.None);

        // Assert
        var okResult = result.Result as OkObjectResult;
        okResult.Should().NotBeNull();
        okResult!.StatusCode.Should().Be(200);
        okResult.Value.Should().BeEquivalentTo(pagedResult);
    }

    [Fact]
    public async Task GetById_ReturnsOkResultWithAssignment()
    {
        // Arrange
        var assignmentId = Guid.NewGuid();
        var expectedAssignment = new AssignmentDto
        {
            Id = assignmentId,
            Title = "Midterm Essay",
            CourseGroupName = "Cohort A",
            DueDate = DateTime.UtcNow.AddDays(5),
            MaxScore = 100
        };

        _assignmentServiceMock.Setup(s => s.GetByIdAsync(assignmentId, _userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedAssignment);

        // Act
        var result = await _controller.GetById(assignmentId, CancellationToken.None);

        // Assert
        var okResult = result.Result as OkObjectResult;
        okResult.Should().NotBeNull();
        okResult!.StatusCode.Should().Be(200);
        okResult.Value.Should().BeEquivalentTo(expectedAssignment);
    }

    [Fact]
    public async Task Create_ReturnsCreatedAtActionResult()
    {
        // Arrange
        var dto = new CreateAssignmentDto
        {
            CourseGroupId = Guid.NewGuid(),
            Title = "Project Alpha",
            Description = "Initial design milestone",
            DueDate = DateTime.UtcNow.AddDays(10),
            MaxScore = 100
        };

        var createdAssignment = new AssignmentDto
        {
            Id = Guid.NewGuid(),
            CourseGroupId = dto.CourseGroupId,
            Title = dto.Title,
            Description = dto.Description,
            DueDate = dto.DueDate,
            MaxScore = dto.MaxScore
        };

        _assignmentServiceMock.Setup(s => s.CreateAsync(dto, _userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(createdAssignment);

        // Act
        var result = await _controller.Create(dto, CancellationToken.None);

        // Assert
        var createdResult = result.Result as CreatedAtActionResult;
        createdResult.Should().NotBeNull();
        createdResult!.StatusCode.Should().Be(201);
        createdResult.ActionName.Should().Be(nameof(AssignmentsController.GetById));
        createdResult.RouteValues.Should().ContainKey("id");
        createdResult.RouteValues!["id"].Should().Be(createdAssignment.Id);
        createdResult.Value.Should().BeEquivalentTo(createdAssignment);
    }

    [Fact]
    public async Task Update_ReturnsOkResultWithUpdatedAssignment()
    {
        // Arrange
        var assignmentId = Guid.NewGuid();
        var dto = new UpdateAssignmentDto
        {
            Title = "Updated Title",
            DueDate = DateTime.UtcNow.AddDays(12),
            MaxScore = 90
        };

        var updatedAssignment = new AssignmentDto
        {
            Id = assignmentId,
            Title = "Updated Title",
            DueDate = dto.DueDate,
            MaxScore = 90
        };

        _assignmentServiceMock.Setup(s => s.UpdateAsync(assignmentId, dto, _userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(updatedAssignment);

        // Act
        var result = await _controller.Update(assignmentId, dto, CancellationToken.None);

        // Assert
        var okResult = result.Result as OkObjectResult;
        okResult.Should().NotBeNull();
        okResult!.StatusCode.Should().Be(200);
        okResult.Value.Should().BeEquivalentTo(updatedAssignment);
    }

    [Fact]
    public async Task Delete_ReturnsNoContentResult()
    {
        // Arrange
        var assignmentId = Guid.NewGuid();

        _assignmentServiceMock.Setup(s => s.DeleteAsync(assignmentId, _userId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _controller.Delete(assignmentId, CancellationToken.None);

        // Assert
        var noContentResult = result as NoContentResult;
        noContentResult.Should().NotBeNull();
        noContentResult!.StatusCode.Should().Be(204);
        _assignmentServiceMock.Verify(s => s.DeleteAsync(assignmentId, _userId, It.IsAny<CancellationToken>()), Times.Once);
    }
}
