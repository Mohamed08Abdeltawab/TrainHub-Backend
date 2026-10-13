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
        var defaultOrigins = new[]
        {
            "http://localhost:5173",
            "http://localhost:5174",
            "http://localhost:3000",
            "http://127.0.0.1:5173",
            "http://127.0.0.1:5174"
        };
        var origins = (configuredOrigins != null && configuredOrigins.Length > 0)
            ? configuredOrigins
            : defaultOrigins;

        policy.WithOrigins(origins)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

// ── API Services ────────────────────────────────────────────────
builder.Services.AddControllers();
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

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}
else
{
    app.UseHttpsRedirection();
}

app.UseCors("AllowFrontend");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();