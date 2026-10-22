using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SalsCaterSuite.Application.Common.Authorization;
using SalsCaterSuite.Infrastructure.Identity;
using SalsCaterSuite.Infrastructure.Persistence;

namespace SalsCaterSuite.IntegrationTests.Infrastructure;

/// <summary>
/// One disposable SQL Server (LocalDB) database per test run, migrated with the real EF Core
/// migrations and seeded with the real <see cref="IDatabaseSeeder"/> (DEC-019, DEC-018).
///
/// The host is started by this fixture so that no auto-migrate/auto-seed runs during startup; the
/// fixture then migrates and seeds explicitly and provisions the role accounts the authorization
/// scenarios need. Every DB-touching test class shares this fixture through
/// <see cref="IntegrationTestCollection"/>, which also serializes them so they cannot collide.
/// </summary>
public sealed class IntegrationDatabaseFixture : IAsyncLifetime
{
    public const string CashierUserName = "cashier.integration@salscatersuite.test";
    public const string CashierPassword = "Cashier@12345";

    public const string ManagerUserName = "manager.integration@salscatersuite.test";
    public const string ManagerPassword = "Manager@12345";

    private IntegrationTestWebApplicationFactory _factory = null!;

    public IntegrationTestWebApplicationFactory Factory => _factory;

    public string DatabaseName => _factory.DatabaseName;

    public string ConnectionString => _factory.ConnectionString;

    public async Task InitializeAsync()
    {
        _factory = new IntegrationTestWebApplicationFactory();

        try
        {
            // Touching Services starts the host (Testing environment => no auto-migrate/seed),
            // then the fixture applies the real migrations and the real seeding routine.
            using (var scope = _factory.Services.CreateScope())
            {
                var seeder = scope.ServiceProvider.GetRequiredService<IDatabaseSeeder>();
                await seeder.ApplyMigrationsAsync();
                await seeder.SeedAsync();
            }

            await ProvisionRoleUserAsync(CashierUserName, CashierPassword, Roles.Cashier);
            await ProvisionRoleUserAsync(ManagerUserName, ManagerPassword, Roles.Manager);

            IntegrationDatabaseRegistry.Record("created", DatabaseName);
        }
        catch
        {
            // Never leave a half-provisioned database behind.
            await DropDatabaseAsync();
            throw;
        }
    }

    public async Task DisposeAsync()
    {
        await DropDatabaseAsync();
        _factory.Dispose();
    }

    public HttpClient CreateClient() => _factory.CreateClient();

    /// <summary>Creates a client authenticated with a real JWT issued by <c>POST /api/auth/login</c>.</summary>
    public async Task<HttpClient> CreateAuthenticatedClientAsync(string userName, string password)
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new { username = userName, password });

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"Login for '{userName}' failed with {(int)response.StatusCode}: " +
                await response.Content.ReadAsStringAsync());
        }

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var token = body.GetProperty("token").GetString();

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    public Task<HttpClient> CreateCashierClientAsync() =>
        CreateAuthenticatedClientAsync(CashierUserName, CashierPassword);

    public Task<HttpClient> CreateManagerClientAsync() =>
        CreateAuthenticatedClientAsync(ManagerUserName, ManagerPassword);

    public Task<HttpClient> CreateAdministratorClientAsync() =>
        CreateAuthenticatedClientAsync(
            IntegrationTestWebApplicationFactory.AdminUserName,
            IntegrationTestWebApplicationFactory.AdminPassword);

    public IServiceScope CreateScope() => _factory.Services.CreateScope();

    public async Task<T> QueryAsync<T>(Func<SalsCaterSuiteDbContext, Task<T>> query)
    {
        using var scope = CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<SalsCaterSuiteDbContext>();
        return await query(context);
    }

    private async Task ProvisionRoleUserAsync(string userName, string password, string role)
    {
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<ApplicationRole>>();

        if (!await roleManager.RoleExistsAsync(role))
        {
            throw new InvalidOperationException(
                $"Role '{role}' was not provisioned by the seeder; the authorization test cannot be meaningful.");
        }

        if (await userManager.FindByNameAsync(userName) is not null)
        {
            return;
        }

        var user = new ApplicationUser
        {
            UserName = userName,
            Email = userName,
            EmailConfirmed = true,
            IsActive = true,
            Firstname = "Integration",
            Lastname = role,
            DateCreated = DateTime.UtcNow
        };

        var created = await userManager.CreateAsync(user, password);
        if (!created.Succeeded)
        {
            throw new InvalidOperationException(
                $"Failed to create integration user '{userName}': " +
                string.Join("; ", created.Errors.Select(e => e.Description)));
        }

        var added = await userManager.AddToRoleAsync(user, role);
        if (!added.Succeeded)
        {
            throw new InvalidOperationException(
                $"Failed to assign role '{role}' to '{userName}': " +
                string.Join("; ", added.Errors.Select(e => e.Description)));
        }
    }

    private async Task DropDatabaseAsync()
    {
        try
        {
            using var scope = _factory.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<SalsCaterSuiteDbContext>();

            var deleted = await context.Database.EnsureDeletedAsync();
            IntegrationDatabaseRegistry.Record(deleted ? "dropped" : "already-absent", DatabaseName);
        }
        catch (Exception ex)
        {
            IntegrationDatabaseRegistry.Record($"drop-failed: {ex.GetType().Name}", DatabaseName);
        }
    }
}
