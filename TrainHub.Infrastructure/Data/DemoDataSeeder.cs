using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using TrainHub.Application.Interfaces;
using TrainHub.Domain.Constants;
using TrainHub.Domain.Models;

namespace TrainHub.Infrastructure.Data;

/// <summary>
/// Seeds realistic multi-tenant demo organizations, courses, cohorts, and personas for TrainHub.
/// Idempotent and safely toggleable via DemoData:Enabled configuration.
/// </summary>
public class DemoDataSeeder : IDemoDataSeeder
{
    private readonly TrainHubDbContext _dbContext;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IConfiguration _configuration;
    private readonly ILogger<DemoDataSeeder> _logger;

    public DemoDataSeeder(
        TrainHubDbContext dbContext,
        UserManager<ApplicationUser> userManager,
        IConfiguration configuration,
        ILogger<DemoDataSeeder> logger)
    {
        _dbContext = dbContext;
        _userManager = userManager;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        var isEnabled = _configuration.GetValue("DemoData:Enabled", false);
        if (!isEnabled)
        {
            _logger.LogInformation("Demo data seeding is disabled via configuration.");
            return;
        }

        // Idempotency check: if SuperAdmin or Al-Amal exists, skip
        var alreadySeeded = await _dbContext.Organizations
            .IgnoreQueryFilters()
            .AnyAsync(o => o.Slug == "al-amal" || o.Slug == "platform", cancellationToken);

        if (alreadySeeded)
        {
            _logger.LogInformation("Demo data already seeded in database. Skipping.");
            return;
        }

        _logger.LogInformation("Starting comprehensive demo data seeding...");

        var now = DateTime.UtcNow;

        // ── 1. Create Organizations ─────────────────────────────────
        var platformOrg = new Organization
        {
            Id = Guid.NewGuid(),
            Name = "TrainHub Platform",
            Slug = "platform",
            Email = "support@trainhub.io",
            Phone = "+1 800 555 0100",
            Address = "100 Innovation Way, Suite 400",
            IsActive = true,
            CreatedAt = now.AddMonths(-6),
            UpdatedAt = now.AddMonths(-6)
        };

        var alAmalOrg = new Organization
        {
            Id = Guid.NewGuid(),
            Name = "Al-Amal Training Center",
            Slug = "al-amal",
            Email = "contact@alamal-center.com",
            Phone = "+20 100 123 4567",
            Address = "15 Tahrir Square, Downtown, Cairo, Egypt",
            IsActive = true,
            CreatedAt = now.AddMonths(-3),
            UpdatedAt = now.AddMonths(-3)
        };

        var nileOrg = new Organization
        {
            Id = Guid.NewGuid(),
            Name = "Nile Skills Academy",
            Slug = "nile-skills",
            Email = "info@nileskills.com",
            Phone = "+20 102 987 6543",
            Address = "42 Corniche El Nile, Maadi, Cairo, Egypt",
            IsActive = true,
            CreatedAt = now.AddMonths(-2),
            UpdatedAt = now.AddMonths(-2)
        };

        await _dbContext.Organizations.AddRangeAsync([platformOrg, alAmalOrg, nileOrg], cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        // ── 2. Create Users & Roles ─────────────────────────────────
        const string demoPassword = "demo1234";

        // SuperAdmin (Platform)
        await CreateUserAsync(platformOrg.Id, "jordan.lee@trainhub.io", "jordan.lee", "Jordan", "Lee", demoPassword, AppRoles.SuperAdmin, cancellationToken);

        // OrgAdmin (Al-Amal)
        await CreateUserAsync(alAmalOrg.Id, "alex.morgan@trainhub.io", "alex.morgan", "Alex", "Morgan", demoPassword, AppRoles.OrgAdmin, cancellationToken);

        // Instructors (Al-Amal)
        var sarahUser = await CreateUserAsync(alAmalOrg.Id, "sarah.jenkins@trainhub.io", "sarah.jenkins", "Sarah", "Jenkins", demoPassword, AppRoles.Instructor, cancellationToken);
        var ahmedUser = await CreateUserAsync(alAmalOrg.Id, "ahmed.hassan@alamal.com", "ahmed.hassan", "Ahmed", "Hassan", demoPassword, AppRoles.Instructor, cancellationToken);
        var nourUser = await CreateUserAsync(alAmalOrg.Id, "nour.eldin@alamal.com", "nour.eldin", "Nour", "El-Din", demoPassword, AppRoles.Instructor, cancellationToken);

        // Students (Al-Amal)
        var davidUser = await CreateUserAsync(alAmalOrg.Id, "david.kim@trainhub.io", "david.kim", "David", "Kim", demoPassword, AppRoles.Student, cancellationToken);
        var laylaUser = await CreateUserAsync(alAmalOrg.Id, "layla.mahmoud@alamal.com", "layla.mahmoud", "Layla", "Mahmoud", demoPassword, AppRoles.Student, cancellationToken);
        var omarUser = await CreateUserAsync(alAmalOrg.Id, "omar.khaled@alamal.com", "omar.khaled", "Omar", "Khaled", demoPassword, AppRoles.Student, cancellationToken);
        var youssefUser = await CreateUserAsync(alAmalOrg.Id, "youssef.ali@alamal.com", "youssef.ali", "Youssef", "Ali", demoPassword, AppRoles.Student, cancellationToken);
        var fatimaUser = await CreateUserAsync(alAmalOrg.Id, "fatima.zahra@alamal.com", "fatima.zahra", "Fatima", "Zahra", demoPassword, AppRoles.Student, cancellationToken);

        // OrgAdmin (Nile Skills Academy - multi-tenant testing)
        await CreateUserAsync(nileOrg.Id, "tariq.mansour@nileskills.com", "tariq.mansour", "Tariq", "Mansour", demoPassword, AppRoles.OrgAdmin, cancellationToken);

        // ── 3. Create Domain Instructors ────────────────────────────
        var sarahInstructor = Instructor.Create(sarahUser.Id, alAmalOrg.Id, "Full-Stack Web Development", "Lead instructor with 8+ years of production experience in React, TypeScript, and .NET Core.");
        var ahmedInstructor = Instructor.Create(ahmedUser.Id, alAmalOrg.Id, "Data Science & Python", "Senior Data Scientist specializing in machine learning and data pipelines.");
        var nourInstructor = Instructor.Create(nourUser.Id, alAmalOrg.Id, "DevOps & Cloud Engineering", "Cloud Architect experienced in Kubernetes, Docker, and CI/CD.");

        await _dbContext.Instructors.AddRangeAsync([sarahInstructor, ahmedInstructor, nourInstructor], cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        // ── 4. Create Domain Students ───────────────────────────────
        var davidStudent = Student.Create(davidUser.Id, alAmalOrg.Id, "STU-2026-001", "+1 555-0199", "2026", new DateOnly(2001, 3, 14), "Enrolled in Full-Stack program");
        var laylaStudent = Student.Create(laylaUser.Id, alAmalOrg.Id, "STU-2026-002", "+20 101 234 5678", "2026", new DateOnly(2002, 7, 21));
        var omarStudent = Student.Create(omarUser.Id, alAmalOrg.Id, "STU-2026-003", "+20 102 345 6789", "2026", new DateOnly(2000, 11, 5));
        var youssefStudent = Student.Create(youssefUser.Id, alAmalOrg.Id, "STU-2026-004", "+20 103 456 7890", "2026", new DateOnly(2003, 1, 19));
        var fatimaStudent = Student.Create(fatimaUser.Id, alAmalOrg.Id, "STU-2026-005", "+20 104 567 8901", "2026", new DateOnly(2001, 9, 30));

        await _dbContext.Students.AddRangeAsync([davidStudent, laylaStudent, omarStudent, youssefStudent, fatimaStudent], cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        // ── 5. Create Courses ───────────────────────────────────────
        var course1 = Course.Create(alAmalOrg.Id, "Full-Stack Web Development with React & .NET", "Comprehensive software engineering program covering React 19, TypeScript, Clean Architecture, and ASP.NET Core Web API.", "Software Engineering", 1200m, 80);
        var course2 = Course.Create(alAmalOrg.Id, "Applied Python & Machine Learning", "Hands-on data analysis, feature engineering, and machine learning models with Python, Pandas, and Scikit-learn.", "Data Science", 1500m, 60);
        var course3 = Course.Create(alAmalOrg.Id, "DevOps & Cloud Engineering", "Modern infrastructure automation, container orchestration with Docker & Kubernetes, and automated deployment pipelines.", "DevOps", 1100m, 45);

        var courseNile = Course.Create(nileOrg.Id, "Cybersecurity & Network Defense", "Foundations of information security, vulnerability assessment, and defensive architecture.", "Cybersecurity", 1400m, 50);

        await _dbContext.Courses.AddRangeAsync([course1, course2, course3, courseNile], cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        // ── 6. Create CourseGroups (Cohorts) ────────────────────────
        var group1 = CourseGroup.Create(alAmalOrg.Id, course1.Id, "Fall 2026 Cohort A", new DateOnly(2026, 9, 15), new DateOnly(2026, 12, 15), "Sundays & Tuesdays, 6:00 PM - 9:00 PM", sarahInstructor.Id, 25, CourseGroupConstants.Statuses.Active);
        var group2 = CourseGroup.Create(alAmalOrg.Id, course2.Id, "Winter 2026 Cohort B", new DateOnly(2026, 10, 1), new DateOnly(2027, 1, 15), "Mondays & Wednesdays, 5:00 PM - 8:00 PM", ahmedInstructor.Id, 20, CourseGroupConstants.Statuses.Active);
        var group3 = CourseGroup.Create(alAmalOrg.Id, course3.Id, "DevOps FastTrack 2026", new DateOnly(2026, 10, 5), new DateOnly(2026, 11, 30), "Saturdays, 10:00 AM - 4:00 PM", nourInstructor.Id, 15, CourseGroupConstants.Statuses.Active);

        var groupNile = CourseGroup.Create(nileOrg.Id, courseNile.Id, "Cyber 2026-1", new DateOnly(2026, 10, 10), new DateOnly(2027, 2, 10), "Weekends, 10:00 AM - 2:00 PM", null, 20, CourseGroupConstants.Statuses.Active);

        await _dbContext.CourseGroups.AddRangeAsync([group1, group2, group3, groupNile], cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        // ── 7. Create Enrollments ───────────────────────────────────
        var enrollmentDavid = Enrollment.Create(alAmalOrg.Id, group1.Id, davidStudent.Id, 1200m, EnrollmentConstants.Statuses.Active, now.AddMonths(-1));
        var enrollmentLayla = Enrollment.Create(alAmalOrg.Id, group1.Id, laylaStudent.Id, 1200m, EnrollmentConstants.Statuses.Active, now.AddMonths(-1));
        var enrollmentOmar = Enrollment.Create(alAmalOrg.Id, group1.Id, omarStudent.Id, 1200m, EnrollmentConstants.Statuses.Active, now.AddMonths(-1));

        var enrollmentYoussef = Enrollment.Create(alAmalOrg.Id, group2.Id, youssefStudent.Id, 1500m, EnrollmentConstants.Statuses.Active, now.AddDays(-10));
        var enrollmentFatima = Enrollment.Create(alAmalOrg.Id, group2.Id, fatimaStudent.Id, 1500m, EnrollmentConstants.Statuses.Active, now.AddDays(-10));

        await _dbContext.Enrollments.AddRangeAsync([enrollmentDavid, enrollmentLayla, enrollmentOmar, enrollmentYoussef, enrollmentFatima], cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        // ── 8. Create Sessions for Group 1 ──────────────────────────
        var session1 = Session.Create(alAmalOrg.Id, group1.Id, new DateTime(2026, 10, 1, 18, 0, 0, DateTimeKind.Utc), new DateTime(2026, 10, 1, 21, 0, 0, DateTimeKind.Utc), "Lab 101", sarahInstructor.Id, SessionConstants.Statuses.Completed);
        var session2 = Session.Create(alAmalOrg.Id, group1.Id, new DateTime(2026, 10, 8, 18, 0, 0, DateTimeKind.Utc), new DateTime(2026, 10, 8, 21, 0, 0, DateTimeKind.Utc), "Lab 101", sarahInstructor.Id, SessionConstants.Statuses.Completed);
        var session3 = Session.Create(alAmalOrg.Id, group1.Id, new DateTime(2026, 10, 15, 18, 0, 0, DateTimeKind.Utc), new DateTime(2026, 10, 15, 21, 0, 0, DateTimeKind.Utc), "Lab 101", sarahInstructor.Id, SessionConstants.Statuses.Scheduled);
        var session4 = Session.Create(alAmalOrg.Id, group1.Id, new DateTime(2026, 10, 22, 18, 0, 0, DateTimeKind.Utc), new DateTime(2026, 10, 22, 21, 0, 0, DateTimeKind.Utc), "Lab 101", sarahInstructor.Id, SessionConstants.Statuses.Scheduled);

        await _dbContext.Sessions.AddRangeAsync([session1, session2, session3, session4], cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        // ── 9. Create AttendanceRecords ─────────────────────────────
        var attDavid1 = AttendanceRecord.Create(alAmalOrg.Id, session1.Id, davidStudent.Id, AttendanceConstants.Statuses.Present, sarahUser.Id, "On time and engaged", session1.StartTime.AddMinutes(5));
        var attLayla1 = AttendanceRecord.Create(alAmalOrg.Id, session1.Id, laylaStudent.Id, AttendanceConstants.Statuses.Present, sarahUser.Id, "On time", session1.StartTime.AddMinutes(5));
        var attOmar1 = AttendanceRecord.Create(alAmalOrg.Id, session1.Id, omarStudent.Id, AttendanceConstants.Statuses.Late, sarahUser.Id, "Arrived 20 mins late", session1.StartTime.AddMinutes(20));

        var attDavid2 = AttendanceRecord.Create(alAmalOrg.Id, session2.Id, davidStudent.Id, AttendanceConstants.Statuses.Present, sarahUser.Id, "On time", session2.StartTime.AddMinutes(5));
        var attLayla2 = AttendanceRecord.Create(alAmalOrg.Id, session2.Id, laylaStudent.Id, AttendanceConstants.Statuses.Present, sarahUser.Id, "On time", session2.StartTime.AddMinutes(5));
        var attOmar2 = AttendanceRecord.Create(alAmalOrg.Id, session2.Id, omarStudent.Id, AttendanceConstants.Statuses.Present, sarahUser.Id, "On time", session2.StartTime.AddMinutes(5));

        await _dbContext.AttendanceRecords.AddRangeAsync([attDavid1, attLayla1, attOmar1, attDavid2, attLayla2, attOmar2], cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Demo data seeding completed successfully! Seeded 3 organizations, 11 users, 3 instructors, 5 students, 4 courses, 4 cohorts, 5 enrollments, 4 sessions, and 6 attendance records.");
    }

    private async Task<ApplicationUser> CreateUserAsync(
        Guid organizationId,
        string email,
        string userName,
        string firstName,
        string lastName,
        string password,
        string role,
        CancellationToken ct)
    {
        var existing = await _userManager.FindByEmailAsync(email);
        if (existing != null)
        {
            return existing;
        }

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            Email = email,
            NormalizedEmail = email.ToUpperInvariant(),
            UserName = userName,
            NormalizedUserName = userName.ToUpperInvariant(),
            FirstName = firstName,
            LastName = lastName,
            EmailConfirmed = true,
            IsActive = true,
            CreatedAt = DateTime.UtcNow.AddMonths(-2),
            UpdatedAt = DateTime.UtcNow.AddMonths(-2)
        };

        var result = await _userManager.CreateAsync(user, password);
        if (!result.Succeeded)
        {
            var err = string.Join(", ", result.Errors.Select(e => e.Description));
            _logger.LogError("Failed to create user {Email}: {Errors}", email, err);
            throw new InvalidOperationException($"Failed to create user {email}: {err}");
        }

        var roleResult = await _userManager.AddToRoleAsync(user, role);
        if (!roleResult.Succeeded)
        {
            var err = string.Join(", ", roleResult.Errors.Select(e => e.Description));
            _logger.LogError("Failed to assign role '{Role}' to user {Email}: {Errors}", role, email, err);
            throw new InvalidOperationException($"Failed to assign role {role} to {email}: {err}");
        }

        _logger.LogInformation("Successfully created demo user '{Email}' with role '{Role}'.", email, role);
        return user;
    }
}
