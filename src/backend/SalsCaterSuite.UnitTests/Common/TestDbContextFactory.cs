namespace SalsCaterSuite.UnitTests.Common;

using Microsoft.EntityFrameworkCore;
using SalsCaterSuite.Infrastructure.Persistence;

public static class TestDbContextFactory
{
    public static SalsCaterSuiteDbContext Create(string? databaseName = null)
    {
        var dbName = databaseName ?? Guid.NewGuid().ToString();
        var options = new DbContextOptionsBuilder<SalsCaterSuiteDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .EnableSensitiveDataLogging()
            .Options;

        var context = new SalsCaterSuiteDbContext(options);
        context.Database.EnsureCreated();
        return context;
    }

    public static void Destroy(SalsCaterSuiteDbContext context)
    {
        context.Database.EnsureDeleted();
        context.Dispose();
    }
}

