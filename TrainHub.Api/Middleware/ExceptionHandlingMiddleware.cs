using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TrainHub.Application.Common.Exceptions;

namespace TrainHub.Api.Middleware;

/// <summary>
/// Global exception handling middleware producing RFC 7807 ProblemDetails responses.
/// Catches all unhandled exceptions, maps them to appropriate HTTP status codes,
/// and returns a consistent JSON error response.
/// </summary>
public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;
    private readonly IHostEnvironment _environment;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public ExceptionHandlingMiddleware(
        RequestDelegate next,
        ILogger<ExceptionHandlingMiddleware> logger,
        IHostEnvironment environment)
    {
        _next = next;
        _logger = logger;
        _environment = environment;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        // ── Map exception type to HTTP status code and title ─────────
        var (statusCode, title) = MapException(exception);

        // ── Log based on severity ───────────────────────────────────
        LogException(exception, statusCode, context);

        // ── Build the ProblemDetails response ───────────────────────
        var problemDetails = CreateProblemDetails(exception, statusCode, title, context);

        // ── Write the response ──────────────────────────────────────
        if (!context.Response.HasStarted)
        {
            context.Response.ContentType = "application/problem+json";
            context.Response.StatusCode = (int)statusCode;
            await context.Response.WriteAsync(
                JsonSerializer.Serialize(problemDetails, JsonOptions));
        }
        else
        {
            _logger.LogWarning(
                "Response has already started; cannot write ProblemDetails for exception on path {Path}. TraceId: {TraceId}",
                context.Request.Path, context.TraceIdentifier);
        }
    }

    /// <summary>
    /// Maps an exception to the appropriate HTTP status code and RFC 7807 title.
    /// </summary>
    private static (HttpStatusCode StatusCode, string Title) MapException(Exception exception) => exception switch
    {
        ValidationException              => (HttpStatusCode.UnprocessableEntity, "Validation Failed"),
        BadRequestException              => (HttpStatusCode.BadRequest,          "Bad Request"),
        ArgumentException                => (HttpStatusCode.BadRequest,          "Bad Request"),
        UnauthorizedAccessException      => (HttpStatusCode.Unauthorized,        "Unauthorized"),
        ForbiddenAccessException         => (HttpStatusCode.Forbidden,           "Forbidden"),
        NotFoundException                => (HttpStatusCode.NotFound,            "Resource Not Found"),
        ConflictException                => (HttpStatusCode.Conflict,            "Conflict"),
        OperationCanceledException       => (HttpStatusCode.BadRequest,          "Request Cancelled"),
        DbUpdateConcurrencyException     => (HttpStatusCode.Conflict,            "Concurrency Conflict"),
        DbUpdateException                => (HttpStatusCode.Conflict,            "Data Conflict"),
        _                                => (HttpStatusCode.InternalServerError, "An unexpected error occurred")
    };

    /// <summary>
    /// Logs the exception with appropriate severity based on status code.
    /// </summary>
    private void LogException(Exception exception, HttpStatusCode statusCode, HttpContext context)
    {
        var path = context.Request.Path;
        var traceId = context.TraceIdentifier;

        switch (statusCode)
        {
            case HttpStatusCode.InternalServerError:
                _logger.LogError(exception,
                    "Unhandled exception while processing {Method} {Path}. TraceId: {TraceId}",
                    context.Request.Method, path, traceId);
                break;

            case HttpStatusCode.Conflict when exception is DbUpdateException or DbUpdateConcurrencyException:
                _logger.LogError(exception,
                    "Database error while processing {Method} {Path}. TraceId: {TraceId}",
                    context.Request.Method, path, traceId);
                break;

            default:
                _logger.LogWarning(
                    "Handled exception ({StatusCode}): {Message} for {Method} {Path}. TraceId: {TraceId}",
                    (int)statusCode, exception.Message, context.Request.Method, path, traceId);
                break;
        }
    }

    /// <summary>
    /// Creates a <see cref="ProblemDetails"/> object from the exception.
    /// In Development, includes the stack trace. In Production, sensitive details are hidden for 5xx errors.
    /// </summary>
    private ProblemDetails CreateProblemDetails(
        Exception exception,
        HttpStatusCode statusCode,
        string title,
        HttpContext context)
    {
        var isServerError = (int)statusCode >= 500;

        var problemDetails = new ProblemDetails
        {
            Status = (int)statusCode,
            Title = title,
            Detail = isServerError && !_environment.IsDevelopment()
                ? "An internal error occurred. Please try again later."
                : GetExceptionDetail(exception),
            Instance = $"{context.Request.Method} {context.Request.Path}",
            Extensions =
            {
                ["traceId"] = context.TraceIdentifier
            }
        };

        // ── Attach validation errors for front-end form binding ─────
        if (exception is ValidationException validationException
            && validationException.Errors.Count > 0)
        {
            problemDetails.Extensions["errors"] = validationException.Errors;
        }

        // ── Attach stack trace in Development ────────────────────────
        if (_environment.IsDevelopment() && isServerError)
        {
            problemDetails.Extensions["exception"] = exception.GetType().FullName;
            problemDetails.Extensions["stackTrace"] = exception.StackTrace;
        }

        return problemDetails;
    }

    /// <summary>
    /// Extracts a user-friendly detail message from the exception.
    /// </summary>
    private static string GetExceptionDetail(Exception exception) => exception switch
    {
        DbUpdateConcurrencyException =>
            "The record was modified by another user. Please refresh and try again.",
        DbUpdateException =>
            "A database conflict occurred while saving changes. A record with identical unique details may already exist, or a referenced entity is invalid.",
        OperationCanceledException =>
            "The request was cancelled by the client.",
        _ =>
            exception.Message
    };
}
