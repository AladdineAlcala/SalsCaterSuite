using System.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SalsCaterSuite.IntegrationTests.Infrastructure;
using SalsCaterSuite.Infrastructure.Persistence;

namespace SalsCaterSuite.IntegrationTests;

/// <summary>
/// Test 1 — Migration validation (Scenario 11 / DEC-019, DEC-020).
/// The real EF Core migrations provision the schema on a genuinely empty SQL Server database.
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public class MigrationValidationTests
{
    private readonly IntegrationDatabaseFixture _fixture;

    public MigrationValidationTests(IntegrationDatabaseFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task InitialCreate_is_applied_and_no_migration_is_pending()
    {
        using var scope = _fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<SalsCaterSuiteDbContext>();

        var applied = (await context.Database.GetAppliedMigrationsAsync()).ToList();
        applied.Should().Contain(
            m => m.EndsWith("InitialCreate", StringComparison.Ordinal),
            $"the InitialCreate migration must be applied to '{_fixture.DatabaseName}'");

        var pending = (await context.Database.GetPendingMigrationsAsync()).ToList();
        pending.Should().BeEmpty(
            "the fixture applies every migration, and the schema must match the model");
    }

    [Fact]
    public async Task The_expected_tables_exist_in_the_test_database()
    {
        using var scope = _fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<SalsCaterSuiteDbContext>();

        var tables = await GetTableNamesAsync(context);

        tables.Should().Contain(new[]
        {
            "Booking", "Payment", "Refund", "Customer", "Package", "Menu",
            "CourseCategory", "Users", "Roles"
        });
    }

    private static async Task<List<string>> GetTableNamesAsync(SalsCaterSuiteDbContext context)
    {
        var tables = new List<string>();
        var connection = context.Database.GetDbConnection();
        var mustClose = connection.State != ConnectionState.Open;

        if (mustClose)
        {
            await connection.OpenAsync();
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT name FROM sys.tables ORDER BY name";
            await using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                tables.Add(reader.GetString(0));
            }
        }
        finally
        {
            if (mustClose)
            {
                await connection.CloseAsync();
            }
        }

        return tables;
    }
}
