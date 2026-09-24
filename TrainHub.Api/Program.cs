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

// ── API Services ────────────────────────────────────────────────
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();

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

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();