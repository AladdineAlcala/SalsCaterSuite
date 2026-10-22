using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SalsCaterSuite.Application.Common.Authorization;
using SalsCaterSuite.IntegrationTests.Infrastructure;
using SalsCaterSuite.Infrastructure.Persistence;

namespace SalsCaterSuite.IntegrationTests;

/// <summary>
/// Test 2 — Seeding idempotency (Scenario 11 / DEC-018).
/// Runs the real seeding routine twice against a database of its own (never the shared
/// collection database, which accumulates test data) and drops it afterwards.
///
/// It is a member of <see cref="IntegrationTestCollection"/> so that no second host and no second
/// LocalDB database is created concurrently with the shared database; because it owns its unique
/// database it cannot collide with the shared one either way.
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public class SeedingIdempotencyTests
{
    private static readonly string[] ExpectedRoles =
    {
        Roles.Administrator, Roles.Manager, Roles.Cashier, Roles.Accountant
    };

    [Fact]
    public async Task Seeding_a_fresh_database_twice_creates_no_duplicates_and_no_business_data()
    {
        var factory = new IntegrationTestWebApplicationFactory();

        try
        {
            using (var scope = factory.Services.CreateScope())
            {
                var seeder = scope.ServiceProvider.GetRequiredService<IDatabaseSeeder>();
                await seeder.ApplyMigrationsAsync();

                // The routine must be a no-op after the first run.
                await seeder.SeedAsync();
                await seeder.SeedAsync();

                var context = scope.ServiceProvider.GetRequiredService<SalsCaterSuiteDbContext>();

                (await context.Roles.Select(r => r.Name).ToListAsync())
                    .Should().BeEquivalentTo(ExpectedRoles, "exactly the four canonical roles (DEC-017)");

                (await context.Users.CountAsync()).Should().Be(1, "exactly one bootstrap administrator");
                (await context.UserRoles.CountAsync()).Should().Be(1, "the administrator keeps exactly one role");

                (await context.CourseCategories.CountAsync())
                    .Should().Be(6, "the six standard course categories (DEC-018)");

                // No fabricated business data, ever (DEC-018).
                (await context.Bookings.CountAsync()).Should().Be(0);
                (await context.Customers.CountAsync()).Should().Be(0);
                (await context.Payments.CountAsync()).Should().Be(0);
                (await context.Menus.CountAsync()).Should().Be(0);
                (await context.Packages.CountAsync()).Should().Be(0);
            }

            IntegrationDatabaseRegistry.Record("created", factory.DatabaseName);
        }
        finally
        {
            try
            {
                using var scope = factory.Services.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<SalsCaterSuiteDbContext>();
                var deleted = await context.Database.EnsureDeletedAsync();

                IntegrationDatabaseRegistry.Record(
                    deleted ? "dropped" : "already-absent", factory.DatabaseName);
            }
            catch (Exception ex)
            {
                IntegrationDatabaseRegistry.Record(
                    $"drop-failed: {ex.GetType().Name}", factory.DatabaseName);
            }

            factory.Dispose();
        }
    }
}
