using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TrainHub.Application.Interfaces;
using TrainHub.Domain.Models;
using TrainHub.Infrastructure.Authentication;
using TrainHub.Infrastructure.Data;
using TrainHub.Infrastructure.Services;

namespace TrainHub.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// Registers Infrastructure-layer services: DbContext, Identity, token generation, and authentication.
    /// </summary>
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // ── DbContext ───────────────────────────────────────────────
        var connectionString = configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Database connection string 'DefaultConnection' is missing. " +
                "Please configure 'ConnectionStrings:DefaultConnection' in configuration or environment variables.");
        }

        services.AddDbContext<TrainHubDbContext>(options =>
            options.UseSqlServer(connectionString));

        services.AddScoped<ITrainHubDbContext>(sp => sp.GetRequiredService<TrainHubDbContext>());

        // ── ASP.NET Identity ────────────────────────────────────────
        services.AddIdentityCore<ApplicationUser>(options =>
        {
            // Password settings
            options.Password.RequireDigit = true;
            options.Password.RequireLowercase = true;
            options.Password.RequireUppercase = false;
            options.Password.RequireNonAlphanumeric = false;
            options.Password.RequiredLength = 8;

            // Lockout settings
            options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
            options.Lockout.MaxFailedAccessAttempts = 5;
            options.Lockout.AllowedForNewUsers = true;

            // User settings
            options.User.RequireUniqueEmail = false;
        })
        .AddRoles<IdentityRole<Guid>>()
        .AddEntityFrameworkStores<TrainHubDbContext>();

        // ── Tenant-Scoped Identity Validator ────────────────────────
        var defaultUserValidator = services.FirstOrDefault(d => d.ServiceType == typeof(IUserValidator<ApplicationUser>));
        if (defaultUserValidator != null)
        {
            services.Remove(defaultUserValidator);
        }
        services.AddScoped<IUserValidator<ApplicationUser>, OrganizationUserValidator>();

        // ── Role & Demo Data Seeding ─────────────────────────────────
        services.AddScoped<IRoleSeeder, RoleSeeder>();
        services.AddScoped<IDemoDataSeeder, DemoDataSeeder>();

        // ── Tenant Context & HTTP Accessor ──────────────────────────
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentOrganizationService, CurrentOrganizationService>();

        // ── JWT Token Generation & Authentication ───────────────────
        services.AddScoped<IJwtTokenGenerator, JwtTokenGenerator>();
        services.AddJwtAuthentication(configuration);

        return services;
    }
}
