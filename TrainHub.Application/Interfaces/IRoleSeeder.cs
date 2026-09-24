namespace TrainHub.Application.Interfaces;

/// <summary>
/// Service interface responsible for seeding default system roles on application startup.
/// </summary>
public interface IRoleSeeder
{
    /// <summary>
    /// Seeds default roles into the database if they do not exist.
    /// </summary>
    Task SeedAsync(CancellationToken cancellationToken = default);
}
