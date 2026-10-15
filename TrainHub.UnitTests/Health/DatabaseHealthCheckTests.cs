using FluentAssertions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Moq;
using TrainHub.Api.Health;
using TrainHub.Infrastructure.Data;
using TrainHub.UnitTests.Common;

namespace TrainHub.UnitTests.Health;

public class DatabaseHealthCheckTests : IDisposable
{
    private readonly TrainHubDbContext _dbContext;
    private readonly Mock<ILogger<DatabaseHealthCheck>> _loggerMock;
    private readonly DatabaseHealthCheck _healthCheck;

    public DatabaseHealthCheckTests()
    {
        _dbContext = TestHelpers.CreateInMemoryDbContext();
        _loggerMock = new Mock<ILogger<DatabaseHealthCheck>>();
        _healthCheck = new DatabaseHealthCheck(_dbContext, _loggerMock.Object);
    }

    public void Dispose()
    {
        _dbContext.Database.EnsureDeleted();
        _dbContext.Dispose();
    }

    [Fact]
    public async Task CheckHealthAsync_WhenDatabaseCanConnect_ReturnsHealthy()
    {
        // Arrange
        var context = new HealthCheckContext();

        // Act
        var result = await _healthCheck.CheckHealthAsync(context);

        // Assert
        result.Status.Should().Be(HealthStatus.Healthy);
        result.Description.Should().Contain("Database connection is healthy");
    }
}
