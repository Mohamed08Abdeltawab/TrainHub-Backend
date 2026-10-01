using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using TrainHub.Api.Controllers;
using TrainHub.Application.Common.Models;
using TrainHub.Application.Features.Courses.DTOs;
using TrainHub.Application.Interfaces;
using TrainHub.Domain.Constants;

namespace TrainHub.UnitTests.Controllers;

public class CoursesControllerTests
{
    private readonly Mock<ICourseService> _courseServiceMock;
    private readonly Mock<ILogger<CoursesController>> _loggerMock;
    private readonly CoursesController _controller;

    public CoursesControllerTests()
    {
        _courseServiceMock = new Mock<ICourseService>();
        _loggerMock = new Mock<ILogger<CoursesController>>();
        _controller = new CoursesController(_courseServiceMock.Object, _loggerMock.Object);
    }

    [Fact]
    public async Task GetAll_ReturnsOkResultWithPagedResult()
    {
        // Arrange
        var pagedResult = new PagedResult<CourseDto>(
            new List<CourseDto>
            {
                new() { Id = Guid.NewGuid(), Name = "Course 1" },
                new() { Id = Guid.NewGuid(), Name = "Course 2" }
            },
            totalCount: 2,
            pageNumber: 1,
            pageSize: 10);

        _courseServiceMock.Setup(s => s.GetPagedAsync(It.IsAny<CourseQueryParameters>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(pagedResult);

        // Act
        var result = await _controller.GetAll(new CourseQueryParameters(), CancellationToken.None);

        // Assert
        var okResult = result.Result as OkObjectResult;
        okResult.Should().NotBeNull();
        okResult!.StatusCode.Should().Be(200);
        okResult.Value.Should().BeEquivalentTo(pagedResult);
    }

    [Fact]
    public async Task GetById_ReturnsOkResultWithCourse()
    {
        // Arrange
        var courseId = Guid.NewGuid();
        var expectedCourse = new CourseDto
        {
            Id = courseId,
            Name = "Clean Code",
            OrganizationId = Guid.NewGuid(),
            Status = CourseConstants.Statuses.Active,
            StatusName = "Active"
        };

        _courseServiceMock.Setup(s => s.GetByIdAsync(courseId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedCourse);

        // Act
        var result = await _controller.GetById(courseId, CancellationToken.None);

        // Assert
        var okResult = result.Result as OkObjectResult;
        okResult.Should().NotBeNull();
        okResult!.StatusCode.Should().Be(200);
        okResult.Value.Should().BeEquivalentTo(expectedCourse);
    }

    [Fact]
    public async Task Create_ReturnsCreatedAtActionResultWithNewCourse()
    {
        // Arrange
        var dto = new CreateCourseDto
        {
            Name = "Domain-Driven Design",
            Category = "Architecture",
            BasePrice = 120m
        };

        var createdCourse = new CourseDto
        {
            Id = Guid.NewGuid(),
            Name = dto.Name,
            Category = dto.Category,
            BasePrice = dto.BasePrice,
            OrganizationId = Guid.NewGuid(),
            Status = CourseConstants.Statuses.Active,
            StatusName = "Active"
        };

        _courseServiceMock.Setup(s => s.CreateAsync(dto, It.IsAny<CancellationToken>()))
            .ReturnsAsync(createdCourse);

        // Act
        var result = await _controller.Create(dto, CancellationToken.None);

        // Assert
        var createdResult = result.Result as CreatedAtActionResult;
        createdResult.Should().NotBeNull();
        createdResult!.StatusCode.Should().Be(201);
        createdResult.ActionName.Should().Be(nameof(CoursesController.GetById));
        createdResult.RouteValues!["id"].Should().Be(createdCourse.Id);
        createdResult.Value.Should().BeEquivalentTo(createdCourse);
    }

    [Fact]
    public async Task Update_ReturnsOkResultWithUpdatedCourse()
    {
        // Arrange
        var courseId = Guid.NewGuid();
        var dto = new UpdateCourseDto
        {
            Name = "Updated Course Title",
            BasePrice = 199m
        };

        var updatedCourse = new CourseDto
        {
            Id = courseId,
            Name = dto.Name,
            BasePrice = dto.BasePrice,
            OrganizationId = Guid.NewGuid()
        };

        _courseServiceMock.Setup(s => s.UpdateAsync(courseId, dto, It.IsAny<CancellationToken>()))
            .ReturnsAsync(updatedCourse);

        // Act
        var result = await _controller.Update(courseId, dto, CancellationToken.None);

        // Assert
        var okResult = result.Result as OkObjectResult;
        okResult.Should().NotBeNull();
        okResult!.StatusCode.Should().Be(200);
        okResult.Value.Should().BeEquivalentTo(updatedCourse);
    }

    [Fact]
    public async Task Delete_ReturnsNoContentResult()
    {
        // Arrange
        var courseId = Guid.NewGuid();
        _courseServiceMock.Setup(s => s.DeleteAsync(courseId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _controller.Delete(courseId, CancellationToken.None);

        // Assert
        var noContentResult = result as NoContentResult;
        noContentResult.Should().NotBeNull();
        noContentResult!.StatusCode.Should().Be(204);

        _courseServiceMock.Verify(s => s.DeleteAsync(courseId, It.IsAny<CancellationToken>()), Times.Once);
    }
}
