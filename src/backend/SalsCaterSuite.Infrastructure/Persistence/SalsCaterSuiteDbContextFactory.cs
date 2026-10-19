using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace SalsCaterSuite.Infrastructure.Persistence;

/// <summary>
/// Deterministic design-time factory for <see cref="SalsCaterSuiteDbContext"/>.
///
/// EF Core tooling (<c>dotnet ef migrations add</c>, <c>database update</c>, <c>script</c>,
/// <c>has-pending-model-changes</c>) uses this factory instead of booting the WebApi host,
/// so migrations never depend on application startup, configuration or JWT secrets.
///
/// Connection resolution (DEC-020 — tooling targets the LocalDB dev/test database only):
///   1. Environment variable <c>SALS_DB_CONNECTION</c> when set (CI / scripting override);
///   2. otherwise LocalDB <c>SalsCaterSuiteDb</c>.
/// </summary>
public class SalsCaterSuiteDbContextFactory : IDesignTimeDbContextFactory<SalsCaterSuiteDbContext>
{
    /// <summary>
    /// Default design-time target: the developer LocalDB instance. Never a shared or
    /// production server — migrations are applied here by explicit operator action only.
    /// </summary>
    private const string DefaultConnectionString =
        "Server=(localdb)\\mssqllocaldb;Database=SalsCaterSuiteDb;Trusted_Connection=True;MultipleActiveResultSets=true";

    public SalsCaterSuiteDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("SALS_DB_CONNECTION");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            connectionString = DefaultConnectionString;
        }

        var optionsBuilder = new DbContextOptionsBuilder<SalsCaterSuiteDbContext>()
            .UseSqlServer(
                connectionString,
                sqlOptions => sqlOptions.MigrationsAssembly(typeof(SalsCaterSuiteDbContext).Assembly.FullName));

        return new SalsCaterSuiteDbContext(optionsBuilder.Options);
    }
}
