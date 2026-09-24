using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using TrainHub.Application.Interfaces;
using TrainHub.Domain.Constants;

namespace TrainHub.Infrastructure.Data;

/// <summary>
/// Seeds default roles into the database on application startup.
/// </summary>
public class RoleSeeder : IRoleSeeder
{
    private readonly RoleManager<IdentityRole<Guid>> _roleManager;
    private readonly ILogger<RoleSeeder> _logger;

    public RoleSeeder(
        RoleManager<IdentityRole<Guid>> roleManager,
        ILogger<RoleSeeder> logger)
    {
        _roleManager = roleManager;
        _logger = logger;
    }

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting role seeding process.");

        foreach (var roleName in AppRoles.All)
        {
            var exists = await _roleManager.RoleExistsAsync(roleName);
            if (!exists)
            {
                var role = new IdentityRole<Guid>
                {
                    Name = roleName,
                    NormalizedName = roleName.ToUpperInvariant()
                };

                var result = await _roleManager.CreateAsync(role);
                if (result.Succeeded)
                {
                    _logger.LogInformation("Successfully seeded role '{RoleName}'.", roleName);
                }
                else
                {
                    var errors = string.Join("; ", result.Errors.Select(e => e.Description));
                    _logger.LogError("Failed to seed role '{RoleName}'. Errors: {Errors}", roleName, errors);
                }
            }
            else
            {
                _logger.LogDebug("Role '{RoleName}' already exists. Skipping.", roleName);
            }
        }

        _logger.LogInformation("Role seeding process completed.");
    }
}
