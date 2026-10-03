using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using TrainHub.Api.Controllers;
using TrainHub.Application.Common.Models;
using TrainHub.Application.Features.Enrollments.DTOs;
using TrainHub.Application.Interfaces;

namespace TrainHub.UnitTests.Controllers;

public class EnrollmentsControllerTests
{
    private readonly Mock<IEnrollmentService> _enrollmentServiceMock;
    private readonly Mock<ILogger<EnrollmentsController>> _loggerMock;
    private readonly EnrollmentsController _controller;

    public EnrollmentsControllerTests()
    {
        _enrollmentServiceMock = new Mock<IEnrollmentService>();
        _loggerMock = new Mock<ILogger<EnrollmentsController>>();
        _controller = new EnrollmentsController(_enrollmentServiceMock.Object, _loggerMock.Object);
    }

    [Fact]
    public async Task GetAll_ReturnsOkResultWithPagedResult()
    {
        // Arrange
        var pagedResult = new PagedResult<EnrollmentDto>(
            new List<EnrollmentDto>
            {
                new() { Id = Guid.NewGuid(), CourseGroupName = "Group A", StudentCode = "STU-01" },
                new() { Id = Guid.NewGuid(), CourseGroupName = "Group B", StudentCode = "STU-02" }
            },
            totalCount: 2,
            pageNumber: 1,
            pageSize: 10);

        _enrollmentServiceMock.Setup(s => s.GetPagedAsync(It.IsAny<EnrollmentQueryParameters>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(pagedResult);

        // Act
        var result = await _controller.GetAll(new EnrollmentQueryParameters(), CancellationToken.None);

        // Assert
        var okResult = result.Result as OkObjectResult;
        okResult.Should().NotBeNull();
        okResult!.StatusCode.Should().Be(200);
        okResult.Value.Should().BeEquivalentTo(pagedResult);
    }

    [Fact]
    public async Task GetById_ReturnsOkResultWithEnrollment()
    {
        // Arrange
        var enrollmentId = Guid.NewGuid();
        var expectedEnrollment = new EnrollmentDto
        {
            Id = enrollmentId,
            CourseGroupName = "Group A",
            StudentCode = "STU-01"
        };

        _enrollmentServiceMock.Setup(s => s.GetByIdAsync(enrollmentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedEnrollment);

        // Act
        var result = await _controller.GetById(enrollmentId, CancellationToken.None);

        // Assert
        var okResult = result.Result as OkObjectResult;
        okResult.Should().NotBeNull();
        okResult!.StatusCode.Should().Be(200);
        okResult.Value.Should().BeEquivalentTo(expectedEnrollment);
    }

    [Fact]
    public async Task Create_ReturnsCreatedAtActionResult()
    {
        // Arrange
        var dto = new CreateEnrollmentDto
        {
            CourseGroupId = Guid.NewGuid(),
            StudentId = Guid.NewGuid(),
            PaidAmount = 100m
        };

        var createdEnrollment = new EnrollmentDto
        {
            Id = Guid.NewGuid(),
            CourseGroupId = dto.CourseGroupId,
            StudentId = dto.StudentId,
            PaidAmount = 100m
        };

        _enrollmentServiceMock.Setup(s => s.CreateAsync(dto, It.IsAny<CancellationToken>()))
            .ReturnsAsync(createdEnrollment);

        // Act
        var result = await _controller.Create(dto, CancellationToken.None);

        // Assert
        var createdResult = result.Result as CreatedAtActionResult;
        createdResult.Should().NotBeNull();
        createdResult!.StatusCode.Should().Be(201);
        createdResult.ActionName.Should().Be(nameof(EnrollmentsController.GetById));
        createdResult.RouteValues.Should().ContainKey("id");
        createdResult.RouteValues!["id"].Should().Be(createdEnrollment.Id);
        createdResult.Value.Should().BeEquivalentTo(createdEnrollment);
    }

    [Fact]
    public async Task Update_ReturnsOkResultWithUpdatedEnrollment()
    {
        // Arrange
        var enrollmentId = Guid.NewGuid();
        var dto = new UpdateEnrollmentDto
        {
            PaidAmount = 250m,
            Status = 1
        };

        var updatedEnrollment = new EnrollmentDto
        {
            Id = enrollmentId,
            PaidAmount = 250m,
            Status = 1
        };

        _enrollmentServiceMock.Setup(s => s.UpdateAsync(enrollmentId, dto, It.IsAny<CancellationToken>()))
            .ReturnsAsync(updatedEnrollment);

        // Act
        var result = await _controller.Update(enrollmentId, dto, CancellationToken.None);

        // Assert
        var okResult = result.Result as OkObjectResult;
        okResult.Should().NotBeNull();
        okResult!.StatusCode.Should().Be(200);
        okResult.Value.Should().BeEquivalentTo(updatedEnrollment);
    }

    [Fact]
    public async Task UpdateStatus_ReturnsOkResultWithUpdatedEnrollment()
    {
        // Arrange
        var enrollmentId = Guid.NewGuid();
        var dto = new UpdateEnrollmentStatusDto
        {
            Status = 2
        };

        var updatedEnrollment = new EnrollmentDto
        {
            Id = enrollmentId,
            Status = 2
        };

        _enrollmentServiceMock.Setup(s => s.UpdateStatusAsync(enrollmentId, dto, It.IsAny<CancellationToken>()))
            .ReturnsAsync(updatedEnrollment);

        // Act
        var result = await _controller.UpdateStatus(enrollmentId, dto, CancellationToken.None);

        // Assert
        var okResult = result.Result as OkObjectResult;
        okResult.Should().NotBeNull();
        okResult!.StatusCode.Should().Be(200);
        okResult.Value.Should().BeEquivalentTo(updatedEnrollment);
    }

    [Fact]
    public async Task Delete_ReturnsNoContentResult()
    {
        // Arrange
        var enrollmentId = Guid.NewGuid();

        _enrollmentServiceMock.Setup(s => s.DeleteAsync(enrollmentId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _controller.Delete(enrollmentId, CancellationToken.None);

        // Assert
        var noContentResult = result as NoContentResult;
        noContentResult.Should().NotBeNull();
        noContentResult!.StatusCode.Should().Be(204);
        _enrollmentServiceMock.Verify(s => s.DeleteAsync(enrollmentId, It.IsAny<CancellationToken>()), Times.Once);
    }
}
