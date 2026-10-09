using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moq;
using TrainHub.Api.Middleware;
using TrainHub.Application.Common.Exceptions;

namespace TrainHub.UnitTests.Middleware;

public class ExceptionHandlingMiddlewareTests
{
    private readonly Mock<ILogger<ExceptionHandlingMiddleware>> _loggerMock;
    private readonly Mock<IHostEnvironment> _environmentMock;

    public ExceptionHandlingMiddlewareTests()
    {
        _loggerMock = new Mock<ILogger<ExceptionHandlingMiddleware>>();
        _environmentMock = new Mock<IHostEnvironment>();
        _environmentMock.Setup(e => e.EnvironmentName).Returns(Environments.Production);
    }

    private (DefaultHttpContext context, MemoryStream responseBody) CreateHttpContext()
    {
        var context = new DefaultHttpContext();
        var responseBody = new MemoryStream();
        context.Response.Body = responseBody;
        context.TraceIdentifier = "test-trace-id-123";
        return (context, responseBody);
    }

    private static async Task<ProblemDetails?> ReadProblemDetailsAsync(MemoryStream stream)
    {
        stream.Seek(0, SeekOrigin.Begin);
        return await JsonSerializer.DeserializeAsync<ProblemDetails>(
            stream,
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
    }

    [Fact]
    public async Task InvokeAsync_WhenNoException_CallsNextDelegate()
    {
        // Arrange
        var (context, _) = CreateHttpContext();
        var nextCalled = false;
        RequestDelegate next = (ctx) =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };

        var middleware = new ExceptionHandlingMiddleware(next, _loggerMock.Object, _environmentMock.Object);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        nextCalled.Should().BeTrue();
    }

    [Fact]
    public async Task InvokeAsync_WhenValidationException_Returns422WithErrors()
    {
        // Arrange
        var (context, body) = CreateHttpContext();
        var errors = new Dictionary<string, string[]>
        {
            { "Email", new[] { "Email is invalid." } },
            { "Password", new[] { "Password is required." } }
        };
        RequestDelegate next = (_) => throw new ValidationException(errors);

        var middleware = new ExceptionHandlingMiddleware(next, _loggerMock.Object, _environmentMock.Object);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Response.StatusCode.Should().Be((int)HttpStatusCode.UnprocessableEntity);
        context.Response.ContentType.Should().Be("application/problem+json");

        var problem = await ReadProblemDetailsAsync(body);
        problem.Should().NotBeNull();
        problem!.Status.Should().Be(422);
        problem.Title.Should().Be("Validation Failed");
        problem.Extensions.Should().ContainKey("errors");
    }

    [Fact]
    public async Task InvokeAsync_WhenBadRequestException_Returns400()
    {
        // Arrange
        var (context, body) = CreateHttpContext();
        RequestDelegate next = (_) => throw new BadRequestException("Custom bad request message.");

        var middleware = new ExceptionHandlingMiddleware(next, _loggerMock.Object, _environmentMock.Object);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Response.StatusCode.Should().Be((int)HttpStatusCode.BadRequest);
        var problem = await ReadProblemDetailsAsync(body);
        problem!.Status.Should().Be(400);
        problem.Title.Should().Be("Bad Request");
        problem.Detail.Should().Be("Custom bad request message.");
    }

    [Fact]
    public async Task InvokeAsync_WhenBadHttpRequestException_Returns400()
    {
        // Arrange
        var (context, body) = CreateHttpContext();
        RequestDelegate next = (_) => throw new BadHttpRequestException("Malformed body");

        var middleware = new ExceptionHandlingMiddleware(next, _loggerMock.Object, _environmentMock.Object);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Response.StatusCode.Should().Be((int)HttpStatusCode.BadRequest);
        var problem = await ReadProblemDetailsAsync(body);
        problem!.Status.Should().Be(400);
        problem.Title.Should().Be("Bad Request");
        problem.Detail.Should().Be("The request payload or format is invalid.");
    }

    [Fact]
    public async Task InvokeAsync_WhenJsonException_Returns400()
    {
        // Arrange
        var (context, body) = CreateHttpContext();
        RequestDelegate next = (_) => throw new JsonException("Unexpected JSON token");

        var middleware = new ExceptionHandlingMiddleware(next, _loggerMock.Object, _environmentMock.Object);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Response.StatusCode.Should().Be((int)HttpStatusCode.BadRequest);
        var problem = await ReadProblemDetailsAsync(body);
        problem!.Status.Should().Be(400);
        problem.Title.Should().Be("Bad Request");
        problem.Detail.Should().Be("The request body contains invalid JSON.");
    }

    [Fact]
    public async Task InvokeAsync_WhenFormatException_Returns400()
    {
        // Arrange
        var (context, body) = CreateHttpContext();
        RequestDelegate next = (_) => throw new FormatException("Invalid GUID format");

        var middleware = new ExceptionHandlingMiddleware(next, _loggerMock.Object, _environmentMock.Object);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Response.StatusCode.Should().Be((int)HttpStatusCode.BadRequest);
        var problem = await ReadProblemDetailsAsync(body);
        problem!.Status.Should().Be(400);
        problem.Title.Should().Be("Bad Request");
        problem.Detail.Should().Be("One or more input values are not in a valid format.");
    }

    [Fact]
    public async Task InvokeAsync_WhenArgumentException_Returns400()
    {
        // Arrange
        var (context, body) = CreateHttpContext();
        RequestDelegate next = (_) => throw new ArgumentException("Page number must be positive");

        var middleware = new ExceptionHandlingMiddleware(next, _loggerMock.Object, _environmentMock.Object);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Response.StatusCode.Should().Be((int)HttpStatusCode.BadRequest);
        var problem = await ReadProblemDetailsAsync(body);
        problem!.Status.Should().Be(400);
        problem.Title.Should().Be("Bad Request");
        problem.Detail.Should().Be("Page number must be positive");
    }

    [Fact]
    public async Task InvokeAsync_WhenUnauthorizedAccessException_Returns401()
    {
        // Arrange
        var (context, body) = CreateHttpContext();
        RequestDelegate next = (_) => throw new UnauthorizedAccessException("Not logged in");

        var middleware = new ExceptionHandlingMiddleware(next, _loggerMock.Object, _environmentMock.Object);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Response.StatusCode.Should().Be((int)HttpStatusCode.Unauthorized);
        var problem = await ReadProblemDetailsAsync(body);
        problem!.Status.Should().Be(401);
        problem.Title.Should().Be("Unauthorized");
    }

    [Fact]
    public async Task InvokeAsync_WhenForbiddenAccessException_Returns403()
    {
        // Arrange
        var (context, body) = CreateHttpContext();
        RequestDelegate next = (_) => throw new ForbiddenAccessException("Forbidden resource access");

        var middleware = new ExceptionHandlingMiddleware(next, _loggerMock.Object, _environmentMock.Object);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Response.StatusCode.Should().Be((int)HttpStatusCode.Forbidden);
        var problem = await ReadProblemDetailsAsync(body);
        problem!.Status.Should().Be(403);
        problem.Title.Should().Be("Forbidden");
    }

    [Fact]
    public async Task InvokeAsync_WhenNotFoundException_Returns404()
    {
        // Arrange
        var (context, body) = CreateHttpContext();
        RequestDelegate next = (_) => throw new NotFoundException("Student not found");

        var middleware = new ExceptionHandlingMiddleware(next, _loggerMock.Object, _environmentMock.Object);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Response.StatusCode.Should().Be((int)HttpStatusCode.NotFound);
        var problem = await ReadProblemDetailsAsync(body);
        problem!.Status.Should().Be(404);
        problem.Title.Should().Be("Resource Not Found");
        problem.Detail.Should().Be("Student not found");
    }

    [Fact]
    public async Task InvokeAsync_WhenKeyNotFoundException_Returns404()
    {
        // Arrange
        var (context, body) = CreateHttpContext();
        RequestDelegate next = (_) => throw new KeyNotFoundException("Item not found");

        var middleware = new ExceptionHandlingMiddleware(next, _loggerMock.Object, _environmentMock.Object);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Response.StatusCode.Should().Be((int)HttpStatusCode.NotFound);
        var problem = await ReadProblemDetailsAsync(body);
        problem!.Status.Should().Be(404);
        problem.Title.Should().Be("Resource Not Found");
        problem.Detail.Should().Be("The requested resource was not found.");
    }

    [Fact]
    public async Task InvokeAsync_WhenConflictException_Returns409()
    {
        // Arrange
        var (context, body) = CreateHttpContext();
        RequestDelegate next = (_) => throw new ConflictException("Email already exists");

        var middleware = new ExceptionHandlingMiddleware(next, _loggerMock.Object, _environmentMock.Object);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Response.StatusCode.Should().Be((int)HttpStatusCode.Conflict);
        var problem = await ReadProblemDetailsAsync(body);
        problem!.Status.Should().Be(409);
        problem.Title.Should().Be("Conflict");
        problem.Detail.Should().Be("Email already exists");
    }

    [Fact]
    public async Task InvokeAsync_WhenDbUpdateConcurrencyException_Returns409WithSanitizedMessage()
    {
        // Arrange
        var (context, body) = CreateHttpContext();
        RequestDelegate next = (_) => throw new DbUpdateConcurrencyException("Internal EF concurrency exception with DB table details");

        var middleware = new ExceptionHandlingMiddleware(next, _loggerMock.Object, _environmentMock.Object);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Response.StatusCode.Should().Be((int)HttpStatusCode.Conflict);
        var problem = await ReadProblemDetailsAsync(body);
        problem!.Status.Should().Be(409);
        problem.Title.Should().Be("Concurrency Conflict");
        problem.Detail.Should().Be("The record was modified by another user. Please refresh and try again.");
    }

    [Fact]
    public async Task InvokeAsync_WhenDbUpdateException_Returns409WithSanitizedMessage()
    {
        // Arrange
        var (context, body) = CreateHttpContext();
        RequestDelegate next = (_) => throw new DbUpdateException("Violation of UNIQUE KEY constraint 'UQ_Users_Email'. Cannot insert duplicate key in object 'dbo.Users'. The duplicate key value is (test@domain.com).");

        var middleware = new ExceptionHandlingMiddleware(next, _loggerMock.Object, _environmentMock.Object);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Response.StatusCode.Should().Be((int)HttpStatusCode.Conflict);
        var problem = await ReadProblemDetailsAsync(body);
        problem!.Status.Should().Be(409);
        problem.Title.Should().Be("Data Conflict");
        // Verify sensitive SQL/table names are completely hidden
        problem.Detail.Should().Be("A database conflict occurred while saving changes. A record with identical unique details may already exist, or a referenced entity is invalid.");
        problem.Detail.Should().NotContain("dbo.Users");
        problem.Detail.Should().NotContain("UQ_Users_Email");
    }

    [Fact]
    public async Task InvokeAsync_WhenUnhandledException_InProduction_HidesInternalDetailsAndStackTrace()
    {
        // Arrange
        _environmentMock.Setup(e => e.EnvironmentName).Returns(Environments.Production);
        var (context, body) = CreateHttpContext();
        RequestDelegate next = (_) => throw new InvalidOperationException("Sensitive internal connection string: Server=secret;Database=prod;User Id=sa;Password=supersecret;");

        var middleware = new ExceptionHandlingMiddleware(next, _loggerMock.Object, _environmentMock.Object);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Response.StatusCode.Should().Be((int)HttpStatusCode.InternalServerError);
        var problem = await ReadProblemDetailsAsync(body);
        problem!.Status.Should().Be(500);
        problem.Title.Should().Be("An unexpected error occurred");
        problem.Detail.Should().Be("An internal error occurred. Please try again later.");
        problem.Detail.Should().NotContain("secret");
        problem.Detail.Should().NotContain("Password");
        problem.Extensions.Should().NotContainKey("stackTrace");
        problem.Extensions.Should().NotContainKey("exception");
    }

    [Fact]
    public async Task InvokeAsync_WhenUnhandledException_InDevelopment_IncludesStackTrace()
    {
        // Arrange
        _environmentMock.Setup(e => e.EnvironmentName).Returns(Environments.Development);
        var (context, body) = CreateHttpContext();
        RequestDelegate next = (_) => throw new InvalidOperationException("Debug error details for developer");

        var middleware = new ExceptionHandlingMiddleware(next, _loggerMock.Object, _environmentMock.Object);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Response.StatusCode.Should().Be((int)HttpStatusCode.InternalServerError);
        var problem = await ReadProblemDetailsAsync(body);
        problem!.Status.Should().Be(500);
        problem.Title.Should().Be("An unexpected error occurred");
        problem.Detail.Should().Be("Debug error details for developer");
        problem.Extensions.Should().ContainKey("exception");
        problem.Extensions.Should().ContainKey("stackTrace");
    }
}
