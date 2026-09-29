using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TrainHub.Application.Interfaces;
using TrainHub.Domain.Models;
using TrainHub.Infrastructure.Authentication;
using TrainHub.Infrastructure.Data;

namespace TrainHub.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// Registers Infrastructure-layer services: DbContext, Identity, repositories, and Unit of Work.
    /// </summary>
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // ── DbContext ───────────────────────────────────────────────
        services.AddDbContext<TrainHubDbContext>(options =>
            options.UseSqlServer(
                configuration.GetConnectionString("DefaultConnection")));

        // ── ASP.NET Identity ────────────────────────────────────────
        services.AddIdentityCore<ApplicationUser>(options =>
        {
            // Password settings
            options.Password.RequireDigit = true;
            options.Password.RequireLowercase = true;
            options.Password.RequireUppercase = true;
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

        // ── Unit of Work & Repositories ─────────────────────────────
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>));

        // ── Roles & Identity Services ────────────────────────────────
        services.AddScoped<IRoleService, Services.RoleService>();
        services.AddScoped<IRoleSeeder, RoleSeeder>();

        // ── Auth Services ────────────────────────────────────────────
        services.AddScoped<IAuthService, Services.AuthService>();

        // ── User Services ───────────────────────────────────────────
        services.AddScoped<ICurrentUserService, Services.CurrentUserService>();

        // ── Organization Services ───────────────────────────────────
        services.AddScoped<IOrganizationValidator, Services.OrganizationValidator>();
        services.AddScoped<IOrganizationService, Services.OrganizationService>();
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentOrganizationService, Services.CurrentOrganizationService>();

        // ── Student Services ────────────────────────────────────────
        services.AddScoped<IStudentValidator, Services.StudentValidator>();
        services.AddScoped<IStudentService, Services.StudentService>();

        // ── Instructor Services ─────────────────────────────────────
        services.AddScoped<IInstructorValidator, Services.InstructorValidator>();
        services.AddScoped<IInstructorService, Services.InstructorService>();

        // ── JWT Authentication ──────────────────────────────────────
        services.AddJwtAuthentication(configuration);

        return services;
    }
}
