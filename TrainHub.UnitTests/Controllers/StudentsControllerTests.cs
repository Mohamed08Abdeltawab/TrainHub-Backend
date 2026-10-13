using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using TrainHub.Api.Controllers;
using TrainHub.Application.Features.Students.DTOs;
using TrainHub.Application.Interfaces;

namespace TrainHub.UnitTests.Controllers;

public class StudentsControllerTests
{
    private readonly Mock<IStudentService> _studentServiceMock;
    private readonly Mock<ILogger<StudentsController>> _loggerMock;
    private readonly StudentsController _controller;

    public StudentsControllerTests()
    {
        _studentServiceMock = new Mock<IStudentService>();
        _loggerMock = new Mock<ILogger<StudentsController>>();
        _controller = new StudentsController(_studentServiceMock.Object, _loggerMock.Object);
    }

    [Fact]
    public async Task GetAll_ReturnsOkResultWithPagedResult()
    {
        // Arrange
        var pagedResult = new TrainHub.Application.Common.Models.PagedResult<StudentDto>(
            new List<StudentDto>
            {
                new() { Id = Guid.NewGuid(), StudentCode = "STU-001" },
                new() { Id = Guid.NewGuid(), StudentCode = "STU-002" }
            },
            totalCount: 2,
            pageNumber: 1,
            pageSize: 10);

        _studentServiceMock.Setup(s => s.GetPagedAsync(It.IsAny<StudentQueryParameters>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(pagedResult);

        // Act
        var result = await _controller.GetAll(new StudentQueryParameters(), CancellationToken.None);

        // Assert
        var okResult = result.Result as OkObjectResult;
        okResult.Should().NotBeNull();
        okResult!.StatusCode.Should().Be(200);
        okResult.Value.Should().BeEquivalentTo(pagedResult);
    }

    [Fact]
    public async Task GetById_ReturnsOkResultWithStudent()
    {
        // Arrange
        var studentId = Guid.NewGuid();
        var expectedStudent = new StudentDto
        {
            Id = studentId,
            StudentCode = "STU-001",
            UserId = Guid.NewGuid(),
            OrganizationId = Guid.NewGuid()
        };

        _studentServiceMock.Setup(s => s.GetByIdAsync(studentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedStudent);

        // Act
        var result = await _controller.GetById(studentId, CancellationToken.None);

        // Assert
        var okResult = result.Result as OkObjectResult;
        okResult.Should().NotBeNull();
        okResult!.StatusCode.Should().Be(200);
        okResult.Value.Should().BeEquivalentTo(expectedStudent);
    }

    [Fact]
    public async Task Create_ValidDto_ReturnsCreatedAtActionResult()
    {
        // Arrange
        var dto = new CreateStudentDto
        {
            UserId = Guid.NewGuid(),
            StudentCode = "STU-001",
            GuardianPhone = "+1234567890",
            AcademicYear = "2026"
        };

        var createdStudent = new StudentDto
        {
            Id = Guid.NewGuid(),
            UserId = dto.UserId,
            StudentCode = dto.StudentCode,
            GuardianPhone = dto.GuardianPhone,
            AcademicYear = dto.AcademicYear
        };

        _studentServiceMock.Setup(s => s.CreateAsync(dto, It.IsAny<CancellationToken>()))
            .ReturnsAsync(createdStudent);

        // Act
        var result = await _controller.Create(dto, CancellationToken.None);

        // Assert
        var createdResult = result.Result as CreatedAtActionResult;
        createdResult.Should().NotBeNull();
        createdResult!.StatusCode.Should().Be(201);
        createdResult.ActionName.Should().Be(nameof(StudentsController.GetById));
        createdResult.RouteValues!["id"].Should().Be(createdStudent.Id);
        createdResult.Value.Should().BeEquivalentTo(createdStudent);
    }

    [Fact]
    public async Task Update_ValidDto_ReturnsOkResultWithUpdatedStudent()
    {
        // Arrange
        var studentId = Guid.NewGuid();
        var dto = new UpdateStudentDto
        {
            StudentCode = "STU-UPDATED",
            GuardianPhone = "+9876543210"
        };

        var updatedStudent = new StudentDto
        {
            Id = studentId,
            StudentCode = dto.StudentCode,
            GuardianPhone = dto.GuardianPhone
        };

        _studentServiceMock.Setup(s => s.UpdateAsync(studentId, dto, It.IsAny<CancellationToken>()))
            .ReturnsAsync(updatedStudent);

        // Act
        var result = await _controller.Update(studentId, dto, CancellationToken.None);

        // Assert
        var okResult = result.Result as OkObjectResult;
        okResult.Should().NotBeNull();
        okResult!.StatusCode.Should().Be(200);
        okResult.Value.Should().BeEquivalentTo(updatedStudent);
    }

    [Fact]
    public async Task Delete_ExistingStudent_ReturnsNoContentResult()
    {
        // Arrange
        var studentId = Guid.NewGuid();
        _studentServiceMock.Setup(s => s.DeleteAsync(studentId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _controller.Delete(studentId, CancellationToken.None);

        // Assert
        var noContentResult = result as NoContentResult;
        noContentResult.Should().NotBeNull();
        noContentResult!.StatusCode.Should().Be(204);
        _studentServiceMock.Verify(s => s.DeleteAsync(studentId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetMyCohorts_AuthenticatedUser_ReturnsOkWithCohorts()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var cohorts = new List<TrainHub.Application.Features.CourseGroups.DTOs.CourseGroupDto>
        {
            new() { Id = Guid.NewGuid(), Name = "Cohort A", CourseName = "Web Dev" }
        };

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext
            {
                User = new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity(
                [
                    new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier, userId.ToString())
                ]))
            }
        };

        _studentServiceMock.Setup(s => s.GetMyCohortsAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(cohorts);

        // Act
        var result = await _controller.GetMyCohorts(CancellationToken.None);

        // Assert
        var okResult = result.Result as OkObjectResult;
        okResult.Should().NotBeNull();
        okResult!.StatusCode.Should().Be(200);
        okResult.Value.Should().BeEquivalentTo(cohorts);
    }

    [Fact]
    public async Task GetMySessions_AuthenticatedUser_ReturnsOkWithSessions()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var sessions = new List<TrainHub.Application.Features.Sessions.DTOs.SessionDto>
        {
            new() { Id = Guid.NewGuid(), CourseGroupName = "Cohort A", Room = "Room 101" }
        };

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext
            {
                User = new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity(
                [
                    new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier, userId.ToString())
                ]))
            }
        };

        _studentServiceMock.Setup(s => s.GetMySessionsAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sessions);

        // Act
        var result = await _controller.GetMySessions(CancellationToken.None);

        // Assert
        var okResult = result.Result as OkObjectResult;
        okResult.Should().NotBeNull();
        okResult!.StatusCode.Should().Be(200);
        okResult.Value.Should().BeEquivalentTo(sessions);
    }
}
