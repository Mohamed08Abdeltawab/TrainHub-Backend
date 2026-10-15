using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using TrainHub.Application.Common.Security;
using TrainHub.Application.Interfaces;
using TrainHub.Application.Services;
using TrainHub.Domain.Constants;
using TrainHub.Domain.Models;
using TrainHub.Infrastructure.Authentication;
using TrainHub.Infrastructure.Data;
using TrainHub.Infrastructure.Services;

namespace TrainHub.IntegrationTests.Infrastructure;

/// <summary>
/// Shared test fixture managing automated integration tests against a real, dedicated SQL Server test database.
/// Strictly enforces safety constraints to prevent accidental execution against production or developer databases.
/// </summary>
public class SqlServerTestFixture : IAsyncLifetime
{
    public const string TestDatabaseName = "TrainHub_IntegrationTests_Db";
    public const string ConnectionString = "Server=.;Database=TrainHub_IntegrationTests_Db;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true;";

    private readonly ServiceProvider _serviceProvider;

    public SqlServerTestFixture()
    {
        ValidateSafeConnectionString(ConnectionString);

        var services = new ServiceCollection();

        services.AddLogging(b => b.AddConsole().SetMinimumLevel(LogLevel.Warning));

        services.AddDbContext<TrainHubDbContext>(options =>
            options.UseSqlServer(ConnectionString));

        services.AddScoped<ITrainHubDbContext>(sp => sp.GetRequiredService<TrainHubDbContext>());

        // ── Identity Setup matching production config ──
        services.AddIdentityCore<ApplicationUser>(options =>
        {
            options.Password.RequireDigit = true;
            options.Password.RequireLowercase = true;
            options.Password.RequireUppercase = false;
            options.Password.RequireNonAlphanumeric = false;
            options.Password.RequiredLength = 8;
            options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
            options.Lockout.MaxFailedAccessAttempts = 5;
            options.Lockout.AllowedForNewUsers = true;
            options.User.RequireUniqueEmail = false;
        })
        .AddRoles<IdentityRole<Guid>>()
        .AddEntityFrameworkStores<TrainHubDbContext>();

        // Register custom tenant-scoped user validator
        var userValidatorDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IUserValidator<ApplicationUser>));
        if (userValidatorDescriptor != null)
        {
            services.Remove(userValidatorDescriptor);
        }
        services.AddScoped<IUserValidator<ApplicationUser>, OrganizationUserValidator>();

        // ── Auth & Domain Services ──
        services.AddScoped<IJwtTokenGenerator, JwtTokenGenerator>();
        services.AddSingleton(Options.Create(new JwtSettings
        {
            Secret = "SuperSecretIntegrationTestingKeyWithAtLeast32BytesLength1234567890",
            Issuer = "TrainHubTestIssuer",
            Audience = "TrainHubTestAudience",
            ExpiryMinutes = 60,
            RefreshTokenExpiryDays = 7
        }));

        services.AddScoped<IOrganizationValidator, OrganizationValidator>();
        services.AddScoped<IEnrollmentValidator, EnrollmentValidator>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IEnrollmentService, EnrollmentService>();
        services.AddScoped<IRoleSeeder, RoleSeeder>();

        // Context mocks for testing
        services.AddScoped<ICurrentOrganizationService, TestCurrentOrganizationService>();
        services.AddSingleton<IHttpContextAccessor>(new HttpContextAccessor());

        _serviceProvider = services.BuildServiceProvider();
    }

    public async Task InitializeAsync()
    {
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TrainHubDbContext>();

        // Fail-safe check on target database name before migration
        var currentDbName = dbContext.Database.GetDbConnection().Database;
        ValidateSafeConnectionString(currentDbName);

        // Apply all EF Core migrations to ensure exact production schema on SQL Server
        await dbContext.Database.MigrateAsync();

        // Seed default roles
        var roleSeeder = scope.ServiceProvider.GetRequiredService<IRoleSeeder>();
        await roleSeeder.SeedAsync();
    }

    public async Task DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
    }

    public IServiceScope CreateScope(Guid? organizationId = null)
    {
        var scope = _serviceProvider.CreateScope();
        if (organizationId.HasValue)
        {
            var orgService = (TestCurrentOrganizationService)scope.ServiceProvider.GetRequiredService<ICurrentOrganizationService>();
            orgService.SetOrganizationId(organizationId.Value);
        }
        return scope;
    }

    public TrainHubDbContext CreateNewDbContext(Guid? organizationId = null)
    {
        var options = new DbContextOptionsBuilder<TrainHubDbContext>()
            .UseSqlServer(ConnectionString)
            .Options;

        var orgService = new TestCurrentOrganizationService();
        if (organizationId.HasValue)
        {
            orgService.SetOrganizationId(organizationId.Value);
        }

        return new TrainHubDbContext(options, orgService);
    }

    public static void ValidateSafeConnectionString(string connectionStringOrDbName)
    {
        if (string.IsNullOrWhiteSpace(connectionStringOrDbName))
        {
            throw new InvalidOperationException("CRITICAL SAFETY CHECK FAILED: Connection string or database name is empty.");
        }

        if (!connectionStringOrDbName.Contains("Test", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "CRITICAL SAFETY CHECK FAILED: Refusing to run integration tests against a database without 'Test' in its name. " +
                "Actual: " + connectionStringOrDbName);
        }

        if (connectionStringOrDbName.Contains("TrainingCenterSaasDb", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "CRITICAL SAFETY CHECK FAILED: Refusing to run tests against primary development database 'TrainingCenterSaasDb'.");
        }
    }
}

public class TestCurrentOrganizationService : ICurrentOrganizationService
{
    private Guid? _organizationId;

    public Guid? OrganizationId => _organizationId;

    public void SetOrganizationId(Guid orgId)
    {
        _organizationId = orgId;
    }
}
