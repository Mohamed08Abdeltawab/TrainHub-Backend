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
    public async Task GetAll_ReturnsOkResultWithListOfStudents()
    {
        // Arrange
        var students = new List<StudentDto>
        {
            new() { Id = Guid.NewGuid(), StudentCode = "STU-001" },
            new() { Id = Guid.NewGuid(), StudentCode = "STU-002" }
        };

        _studentServiceMock.Setup(s => s.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(students);

        // Act
        var result = await _controller.GetAll(CancellationToken.None);

        // Assert
        var okResult = result.Result as OkObjectResult;
        okResult.Should().NotBeNull();
        okResult!.StatusCode.Should().Be(200);
        okResult.Value.Should().BeEquivalentTo(students);
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
}
