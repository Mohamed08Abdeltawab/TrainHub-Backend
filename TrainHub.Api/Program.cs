using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TrainHub.Api.Middleware;
using TrainHub.Application;
using TrainHub.Application.Interfaces;
using TrainHub.Domain.Constants;
using TrainHub.Infrastructure;
using TrainHub.Infrastructure.Data;

var builder = WebApplication.CreateBuilder(args);

// ── Service Registration (Clean Architecture layers) ────────────
builder.Services
    .AddApplication()
    .AddInfrastructure(builder.Configuration);

// ── Health Checks ───────────────────────────────────────────────
builder.Services.AddHealthChecks();

// ── Authorization Policies ──────────────────────────────────────
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(AppPolicies.RequireSuperAdmin, policy =>
        policy.RequireRole(AppRoles.SuperAdmin));

    options.AddPolicy(AppPolicies.RequireOrgAdmin, policy =>
        policy.RequireRole(AppRoles.OrgAdmin));

    options.AddPolicy(AppPolicies.RequireInstructor, policy =>
        policy.RequireRole(AppRoles.Instructor));

    options.AddPolicy(AppPolicies.RequireStudent, policy =>
        policy.RequireRole(AppRoles.Student));

    options.AddPolicy(AppPolicies.RequireOrgStaff, policy =>
        policy.RequireRole(AppRoles.SuperAdmin, AppRoles.OrgAdmin));

    options.AddPolicy(AppPolicies.RequireCourseStaff, policy =>
        policy.RequireRole(AppRoles.SuperAdmin, AppRoles.OrgAdmin, AppRoles.Instructor));
});

// ── CORS Configuration ─────────────────────────────────────────
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        var configuredOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>();
        var allowedOrigins = (configuredOrigins != null && configuredOrigins.Length > 0)
            ? configuredOrigins
            : new[]
            {
                "http://localhost:5173",
                "http://localhost:5174",
                "http://localhost:3000",
                "http://127.0.0.1:5173",
                "http://127.0.0.1:5174"
            };

        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

// ── API Services ────────────────────────────────────────────────
builder.Services.AddControllers()
    .ConfigureApiBehaviorOptions(options =>
    {
        options.InvalidModelStateResponseFactory = context =>
        {
            var problemDetails = new ValidationProblemDetails(context.ModelState)
            {
                Type = "https://tools.ietf.org/html/rfc7231#section-6.5.1",
                Title = "One or more validation errors occurred.",
                Status = StatusCodes.Status400BadRequest,
                Detail = "Please refer to the errors property for additional details.",
                Instance = $"{context.HttpContext.Request.Method} {context.HttpContext.Request.Path}"
            };
            problemDetails.Extensions["traceId"] = context.HttpContext.TraceIdentifier;

            return new BadRequestObjectResult(problemDetails)
            {
                ContentTypes = { "application/problem+json" }
            };
        };
    });
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer((document, context, cancellationToken) =>
    {
        document.Info = new Microsoft.OpenApi.OpenApiInfo
        {
            Title = "TrainHub Management API",
            Version = "v1",
            Description = "Multi-tenant RESTful API for TrainHub Training Center Management SaaS platform, covering authentication, organization governance, course groups, sessions, student enrollments, and attendance tracking."
        };

        document.Components ??= new Microsoft.OpenApi.OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, Microsoft.OpenApi.IOpenApiSecurityScheme>();
        document.Components.SecuritySchemes["Bearer"] = new Microsoft.OpenApi.OpenApiSecurityScheme
        {
            Type = Microsoft.OpenApi.SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            Description = "Enter your JWT Bearer token in the format: {token}"
        };

        document.Security ??= new List<Microsoft.OpenApi.OpenApiSecurityRequirement>();
        document.Security.Add(new Microsoft.OpenApi.OpenApiSecurityRequirement
        {
            {
                new Microsoft.OpenApi.OpenApiSecuritySchemeReference("Bearer", document, null),
                new List<string>()
            }
        });

        return Task.CompletedTask;
    });
});

// ── Production Environment Validation ──────────────────────────
if (!builder.Environment.IsDevelopment())
{
    var jwtSecret = builder.Configuration["JwtSettings:Secret"] ?? builder.Configuration["Jwt:Secret"];
    if (string.Equals(jwtSecret, "TrainHub_Super_Secret_Key_For_Jwt_Signing_Must_Be_At_Least_32_Bytes_Long!", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "Insecure configuration: Development dummy JWT Secret cannot be used in a Production environment. " +
            "Please set a secure 'JwtSettings__Secret' environment variable.");
    }
}

var app = builder.Build();

// ── Apply Migrations & Seed Default System Roles & Demo Data ────
using (var scope = app.Services.CreateScope())
{
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    try
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<TrainHubDbContext>();
        if (dbContext.Database.IsRelational())
        {
            logger.LogInformation("Applying pending database migrations...");
            await dbContext.Database.MigrateAsync();
        }

        var roleSeeder = scope.ServiceProvider.GetRequiredService<IRoleSeeder>();
        await roleSeeder.SeedAsync();

        var demoSeeder = scope.ServiceProvider.GetRequiredService<IDemoDataSeeder>();
        await demoSeeder.SeedAsync();
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "An error occurred while initializing database or seeding system data on startup.");
    }
}

// ── HTTP Pipeline ─────────────────Before any thing────────────
app.UseMiddleware<ExceptionHandlingMiddleware>();

// ── Security Headers ────────────────────────────────────────────
app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["X-Frame-Options"] = "DENY";
    context.Response.Headers["X-XSS-Protection"] = "0";
    context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    context.Response.Headers["Content-Security-Policy"] = "default-src 'none'; frame-ancestors 'none'";
    context.Response.Headers["X-Permitted-Cross-Domain-Policies"] = "none";

    await next();
});

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}
else
{
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseCors("AllowFrontend");

app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health");
app.MapHealthChecks("/api/health");

app.MapControllers();

app.Run();