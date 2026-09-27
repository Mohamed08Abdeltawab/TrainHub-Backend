using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Moq;
using TrainHub.Domain.Models;
using TrainHub.Infrastructure.Data;

namespace TrainHub.UnitTests.Common;

public static class TestHelpers
{
    public static TrainHubDbContext CreateInMemoryDbContext(string? dbName = null)
    {
        var options = new DbContextOptionsBuilder<TrainHubDbContext>()
            .UseInMemoryDatabase(databaseName: dbName ?? Guid.NewGuid().ToString())
            .Options;

        return new TrainHubDbContext(options);
    }

    public static Mock<UserManager<ApplicationUser>> CreateMockUserManager()
    {
        var store = new Mock<IUserStore<ApplicationUser>>();
        var userManager = new Mock<UserManager<ApplicationUser>>(
            store.Object,
            null!, null!, null!, null!, null!, null!, null!, null!);

        userManager.Setup(m => m.NormalizeEmail(It.IsAny<string>()))
            .Returns<string>(e => e?.ToUpperInvariant() ?? string.Empty);

        return userManager;
    }
}
