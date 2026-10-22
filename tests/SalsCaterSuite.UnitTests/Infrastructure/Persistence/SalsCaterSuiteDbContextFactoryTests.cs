namespace SalsCaterSuite.UnitTests.Infrastructure.Persistence;

using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SalsCaterSuite.Infrastructure.Persistence;
using Xunit;

/// <summary>
/// Proves the design-time factory contract (DEC-020): <c>dotnet ef</c> resolves the
/// context deterministically from the factory, targeting LocalDB by default and honouring
/// the <c>SALS_DB_CONNECTION</c> environment-variable override. No database is contacted.
/// </summary>
public class SalsCaterSuiteDbContextFactoryTests
{
    private const string DefaultDataSource = "(localdb)\\mssqllocaldb";
    private const string DefaultDatabase = "SalsCaterSuiteDb";

    [Fact]
    public void CreateDbContext_falls_back_to_the_localdb_dev_database()
    {
        var original = Environment.GetEnvironmentVariable("SALS_DB_CONNECTION");
        try
        {
            Environment.SetEnvironmentVariable("SALS_DB_CONNECTION", null);

            using var context = new SalsCaterSuiteDbContextFactory().CreateDbContext(Array.Empty<string>());

            // SqlConnection normalises keyword aliases, so assert on the parsed semantics
            // (Data Source / Initial Catalog) rather than the literal string.
            context.Database.GetDbConnection().DataSource.Should().Be(DefaultDataSource);
            context.Database.GetDbConnection().Database.Should().Be(DefaultDatabase);
            context.Database.ProviderName.Should().Be("Microsoft.EntityFrameworkCore.SqlServer");
        }
        finally
        {
            Environment.SetEnvironmentVariable("SALS_DB_CONNECTION", original);
        }
    }

    [Fact]
    public void CreateDbContext_prefers_the_environment_variable_override()
    {
        const string overrideConnectionString =
            "Server=(localdb)\\mssqllocaldb;Database=SalsCaterSuiteDb;Trusted_Connection=True;Application Name=FactoryProbe";

        var original = Environment.GetEnvironmentVariable("SALS_DB_CONNECTION");
        try
        {
            Environment.SetEnvironmentVariable("SALS_DB_CONNECTION", overrideConnectionString);

            using var context = new SalsCaterSuiteDbContextFactory().CreateDbContext(Array.Empty<string>());

            // The override connection string is the one that reached the provider.
            context.Database.GetDbConnection().ConnectionString.Should().Contain("Application Name=FactoryProbe");
            context.Database.GetDbConnection().DataSource.Should().Be(DefaultDataSource);
            context.Database.GetDbConnection().Database.Should().Be(DefaultDatabase);
        }
        finally
        {
            Environment.SetEnvironmentVariable("SALS_DB_CONNECTION", original);
        }
    }
}
