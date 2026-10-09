using Microsoft.AspNetCore.Mvc;
using TrainHub.Api.Middleware;
using TrainHub.Application;
using TrainHub.Application.Interfaces;
using TrainHub.Domain.Constants;
using TrainHub.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// ── Service Registration (Clean Architecture layers) ────────────
builder.Services
    .AddApplication()
    .AddInfrastructure(builder.Configuration);

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
        policy.WithOrigins(
                  "http://localhost:5173",
                  "http://localhost:5174",
                  "http://localhost:3000",
                  "http://127.0.0.1:5173",
                  "http://127.0.0.1:5174")
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

var app = builder.Build();

// ── Seed Default System Roles on Startup ────────────────────────
using (var scope = app.Services.CreateScope())
{
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    try
    {
        var roleSeeder = scope.ServiceProvider.GetRequiredService<IRoleSeeder>();
        await roleSeeder.SeedAsync();
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "An error occurred while seeding system roles on startup.");
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

app.MapControllers();

app.Run();