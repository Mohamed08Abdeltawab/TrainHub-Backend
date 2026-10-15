using Microsoft.Extensions.Diagnostics.HealthChecks;
using TrainHub.Infrastructure.Data;

namespace TrainHub.Api.Health;

public class DatabaseHealthCheck : IHealthCheck
{
    private readonly TrainHubDbContext _dbContext;
    private readonly ILogger<DatabaseHealthCheck> _logger;

    public DatabaseHealthCheck(TrainHubDbContext dbContext, ILogger<DatabaseHealthCheck> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var canConnect = await _dbContext.Database.CanConnectAsync(cancellationToken);
            if (canConnect)
            {
                return HealthCheckResult.Healthy("Database connection is healthy.");
            }

            return HealthCheckResult.Unhealthy("Database connection failed: cannot connect to the configured database.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Database health check probe encountered an error.");
            return HealthCheckResult.Unhealthy("Database health check probe failed.", ex);
        }
    }
}
