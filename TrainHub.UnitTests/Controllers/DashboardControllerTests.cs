using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using TrainHub.Api.Controllers;
using TrainHub.Application.Features.Dashboard.DTOs;
using TrainHub.Application.Interfaces;
using TrainHub.Domain.Constants;

namespace TrainHub.UnitTests.Controllers;

public class DashboardControllerTests
{
    private readonly Mock<IDashboardService> _dashboardServiceMock;
    private readonly Mock<ILogger<DashboardController>> _loggerMock;
    private readonly DashboardController _controller;

    public DashboardControllerTests()
    {
        _dashboardServiceMock = new Mock<IDashboardService>();
        _loggerMock = new Mock<ILogger<DashboardController>>();
        _controller = new DashboardController(_dashboardServiceMock.Object, _loggerMock.Object);
    }

    [Fact]
    public void Controller_HasCorrectAttributes()
    {
        // Assert
        var type = typeof(DashboardController);

        var apiControllerAttr = type.GetCustomAttributes(typeof(ApiControllerAttribute), inherit: false);
        apiControllerAttr.Should().NotBeEmpty();

        var routeAttr = type.GetCustomAttributes(typeof(RouteAttribute), inherit: false)
            .Cast<RouteAttribute>()
            .FirstOrDefault();
        routeAttr.Should().NotBeNull();
        routeAttr!.Template.Should().Be("api/[controller]");

        var authAttr = type.GetCustomAttributes(typeof(AuthorizeAttribute), inherit: false)
            .Cast<AuthorizeAttribute>()
            .FirstOrDefault();
        authAttr.Should().NotBeNull();
    }

    [Fact]
    public async Task GetSummary_ReturnsOkResultWithDashboardSummary()
    {
        // Arrange
        var expectedSummary = new DashboardSummaryDto
        {
            OrganizationId = Guid.NewGuid(),
            Students = new StudentStatisticsDto { TotalStudents = 10, ActiveStudents = 8 },
            Courses = new CourseStatisticsDto { TotalCourses = 5, ActiveCourses = 4 },
            Instructors = new InstructorStatisticsDto { TotalInstructors = 3, ActiveInstructors = 3 },
            Attendance = new AttendanceStatisticsDto { TotalRecords = 20, AttendancePercentage = 95.00m }
        };

        _dashboardServiceMock.Setup(s => s.GetDashboardSummaryAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedSummary);

        // Act
        var result = await _controller.GetSummary(CancellationToken.None);

        // Assert
        var okResult = result.Result as OkObjectResult;
        okResult.Should().NotBeNull();
        okResult!.StatusCode.Should().Be(200);
        okResult.Value.Should().BeEquivalentTo(expectedSummary);
    }

    [Fact]
    public async Task GetStudentStatistics_ReturnsOkResultWithStudentStatistics()
    {
        // Arrange
        var expectedStats = new StudentStatisticsDto
        {
            TotalStudents = 15,
            ActiveStudents = 12,
            InactiveStudents = 3,
            TotalEnrollments = 20,
            ActiveEnrollments = 18
        };

        _dashboardServiceMock.Setup(s => s.GetStudentStatisticsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedStats);

        // Act
        var result = await _controller.GetStudentStatistics(CancellationToken.None);

        // Assert
        var okResult = result.Result as OkObjectResult;
        okResult.Should().NotBeNull();
        okResult!.StatusCode.Should().Be(200);
        okResult.Value.Should().BeEquivalentTo(expectedStats);
    }

    [Fact]
    public async Task GetCourseStatistics_ReturnsOkResultWithCourseStatistics()
    {
        // Arrange
        var expectedStats = new CourseStatisticsDto
        {
            TotalCourses = 8,
            ActiveCourses = 6,
            DraftCourses = 2,
            TotalCourseGroups = 12,
            ActiveCourseGroups = 10
        };

        _dashboardServiceMock.Setup(s => s.GetCourseStatisticsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedStats);

        // Act
        var result = await _controller.GetCourseStatistics(CancellationToken.None);

        // Assert
        var okResult = result.Result as OkObjectResult;
        okResult.Should().NotBeNull();
        okResult!.StatusCode.Should().Be(200);
        okResult.Value.Should().BeEquivalentTo(expectedStats);
    }

    [Fact]
    public async Task GetInstructorStatistics_ReturnsOkResultWithInstructorStatistics()
    {
        // Arrange
        var expectedStats = new InstructorStatisticsDto
        {
            TotalInstructors = 6,
            ActiveInstructors = 5,
            InactiveInstructors = 1,
            AssignedInstructors = 4,
            ActiveAssignedInstructors = 4
        };

        _dashboardServiceMock.Setup(s => s.GetInstructorStatisticsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedStats);

        // Act
        var result = await _controller.GetInstructorStatistics(CancellationToken.None);

        // Assert
        var okResult = result.Result as OkObjectResult;
        okResult.Should().NotBeNull();
        okResult!.StatusCode.Should().Be(200);
        okResult.Value.Should().BeEquivalentTo(expectedStats);
    }

    [Fact]
    public async Task GetAttendanceStatistics_WithoutCourseGroupId_ReturnsOkResult()
    {
        // Arrange
        var expectedStats = new AttendanceStatisticsDto
        {
            TotalRecords = 50,
            PresentCount = 40,
            LateCount = 5,
            AbsentCount = 5,
            AttendancePercentage = 90.00m,
            TotalSessions = 10,
            ScheduledSessions = 8
        };

        _dashboardServiceMock.Setup(s => s.GetAttendanceStatisticsAsync(null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedStats);

        // Act
        var result = await _controller.GetAttendanceStatistics(null, CancellationToken.None);

        // Assert
        var okResult = result.Result as OkObjectResult;
        okResult.Should().NotBeNull();
        okResult!.StatusCode.Should().Be(200);
        okResult.Value.Should().BeEquivalentTo(expectedStats);
    }

    [Fact]
    public async Task GetAttendanceStatistics_WithCourseGroupId_PassesParamToService()
    {
        // Arrange
        var courseGroupId = Guid.NewGuid();
        var expectedStats = new AttendanceStatisticsDto
        {
            CourseGroupId = courseGroupId,
            TotalRecords = 25,
            PresentCount = 20,
            AttendancePercentage = 80.00m
        };

        _dashboardServiceMock.Setup(s => s.GetAttendanceStatisticsAsync(courseGroupId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedStats);

        // Act
        var result = await _controller.GetAttendanceStatistics(courseGroupId, CancellationToken.None);

        // Assert
        var okResult = result.Result as OkObjectResult;
        okResult.Should().NotBeNull();
        okResult!.StatusCode.Should().Be(200);
        okResult.Value.Should().BeEquivalentTo(expectedStats);
        _dashboardServiceMock.Verify(s => s.GetAttendanceStatisticsAsync(courseGroupId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(nameof(DashboardController.GetDashboardContent), "content", typeof(DashboardContentDto))]
    [InlineData(nameof(DashboardController.GetSummary), null, typeof(DashboardSummaryDto))]
    [InlineData(nameof(DashboardController.GetStudentStatistics), "students", typeof(StudentStatisticsDto))]
    [InlineData(nameof(DashboardController.GetCourseStatistics), "courses", typeof(CourseStatisticsDto))]
    [InlineData(nameof(DashboardController.GetInstructorStatistics), "instructors", typeof(InstructorStatisticsDto))]
    [InlineData(nameof(DashboardController.GetAttendanceStatistics), "attendance", typeof(AttendanceStatisticsDto))]
    public void Endpoints_HaveCorrectOpenApiAndRoutingMetadata(string methodName, string? expectedTemplate, Type expectedResponseType)
    {
        var method = typeof(DashboardController).GetMethod(methodName);
        method.Should().NotBeNull();

        var httpGetAttr = method!.GetCustomAttributes(typeof(HttpGetAttribute), inherit: false)
            .Cast<HttpGetAttribute>()
            .FirstOrDefault();
        httpGetAttr.Should().NotBeNull();
        httpGetAttr!.Template.Should().Be(expectedTemplate);

        var produces200 = method.GetCustomAttributes(typeof(ProducesResponseTypeAttribute), inherit: false)
            .Cast<ProducesResponseTypeAttribute>()
            .FirstOrDefault(a => a.StatusCode == 200);
        produces200.Should().NotBeNull();
        produces200!.Type.Should().Be(expectedResponseType);
    }

    [Fact]
    public async Task GetDashboardContent_ReturnsOkResultWithDashboardContent()
    {
        // Arrange
        var expectedContent = new DashboardContentDto
        {
            Tenant = "Test Tenant",
            Plan = "Standard",
            Section = "Overview",
            Title = "Overview Title",
            Description = "Overview Desc"
        };

        _dashboardServiceMock.Setup(s => s.GetDashboardContentAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedContent);

        // Act
        var result = await _controller.GetDashboardContent(CancellationToken.None);

        // Assert
        var okResult = result.Result as OkObjectResult;
        okResult.Should().NotBeNull();
        okResult!.StatusCode.Should().Be(200);
        okResult.Value.Should().BeEquivalentTo(expectedContent);
    }
}
