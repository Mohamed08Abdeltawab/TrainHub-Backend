namespace TrainHub.Application.Interfaces;

/// <summary>
/// Service contract for seeding demo organizations, accounts, and operational data.
/// </summary>
public interface IDemoDataSeeder
{
    /// <summary>
    /// Seeds multi-tenant demo data into the database if not already seeded.
    /// </summary>
    Task SeedAsync(CancellationToken cancellationToken = default);
}
