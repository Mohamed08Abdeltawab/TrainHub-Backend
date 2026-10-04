using Microsoft.Extensions.DependencyInjection;
using TrainHub.Application.Interfaces;
using TrainHub.Application.Services;

namespace TrainHub.Application;

public static class DependencyInjection
{
    /// <summary>
    /// Registers Application-layer services: use cases and validators.
    /// </summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        // ── Auth Services ────────────────────────────────────────────
        services.AddScoped<IAuthService, AuthService>();

        // ── User & Role Services ─────────────────────────────────────
        services.AddScoped<IRoleService, RoleService>();
        services.AddScoped<ICurrentUserService, CurrentUserService>();

        // ── Organization Services ───────────────────────────────────
        services.AddScoped<IOrganizationValidator, OrganizationValidator>();
        services.AddScoped<IOrganizationService, OrganizationService>();

        // ── Student Services ────────────────────────────────────────
        services.AddScoped<IStudentValidator, StudentValidator>();
        services.AddScoped<IStudentService, StudentService>();

        // ── Instructor Services ─────────────────────────────────────
        services.AddScoped<IInstructorValidator, InstructorValidator>();
        services.AddScoped<IInstructorService, InstructorService>();

        // ── Course Services ─────────────────────────────────────────
        services.AddScoped<ICourseValidator, CourseValidator>();
        services.AddScoped<ICourseService, CourseService>();

        // ── CourseGroup Services ────────────────────────────────────
        services.AddScoped<ICourseGroupValidator, CourseGroupValidator>();
        services.AddScoped<ICourseGroupService, CourseGroupService>();

        // ── Enrollment Services ─────────────────────────────────────
        services.AddScoped<IEnrollmentValidator, EnrollmentValidator>();
        services.AddScoped<IEnrollmentService, EnrollmentService>();

        // ── Session Services ────────────────────────────────────────
        services.AddScoped<ISessionValidator, SessionValidator>();
        services.AddScoped<ISessionService, SessionService>();

        return services;
    }
}
