using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using TrainHub.Application.Common.Exceptions;

namespace TrainHub.Api.Middleware;

/// <summary>
/// Global exception handling middleware producing RFC 7807 ProblemDetails responses.
/// </summary>
public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
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
        var (statusCode, title) = exception switch
        {
            NotFoundException => (HttpStatusCode.NotFound, "Resource Not Found"),
            ConflictException => (HttpStatusCode.Conflict, "Conflict"),
            BadRequestException or ArgumentException => (HttpStatusCode.BadRequest, "Bad Request"),
            UnauthorizedAccessException => (HttpStatusCode.Unauthorized, "Unauthorized"),
            _ => (HttpStatusCode.InternalServerError, "An unexpected error occurred")
        };

        if (statusCode == HttpStatusCode.InternalServerError)
        {
            _logger.LogError(exception, "Unhandled exception occurred while processing request to {Path}. TraceId: {TraceId}",
                context.Request.Path, context.TraceIdentifier);
        }
        else
        {
            _logger.LogWarning("Handled business exception ({StatusCode}): {Message} for path {Path}. TraceId: {TraceId}",
                (int)statusCode, exception.Message, context.Request.Path, context.TraceIdentifier);
        }

        var problemDetails = new ProblemDetails
        {
            Status = (int)statusCode,
            Title = title,
            Detail = exception.Message,
            Instance = context.Request.Path,
            Extensions =
            {
                ["traceId"] = context.TraceIdentifier
            }
        };

        context.Response.ContentType = "application/problem+json";
        context.Response.StatusCode = (int)statusCode;

        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        await context.Response.WriteAsync(JsonSerializer.Serialize(problemDetails, options));
    }
}
