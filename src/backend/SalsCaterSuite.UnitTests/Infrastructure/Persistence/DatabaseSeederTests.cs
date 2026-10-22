namespace SalsCaterSuite.UnitTests.Infrastructure.Persistence;

using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using SalsCaterSuite.Application.Common.Authorization;
using SalsCaterSuite.Infrastructure.Identity;
using SalsCaterSuite.Infrastructure.Persistence;
using SalsCaterSuite.Infrastructure.Persistence.Repositories;
using Xunit;

/// <summary>
/// Proves the controlled seeding contract (DEC-017, DEC-018, DEC-020):
/// exactly the four identity roles, one bootstrap administrator and the six standard
/// course categories are created; the routine is idempotent; an existing account is
/// never mutated; no credential is invented outside Development; and no fabricated
/// business data is ever written.
///
/// These tests run on the EF Core InMemory provider so they never touch SQL Server.
/// </summary>
public class DatabaseSeederTests
{
    private static readonly string[] ExpectedRoles =
    {
        Roles.Administrator,
        Roles.Manager,
        Roles.Cashier,
        Roles.Accountant
    };

    private static readonly string[] ExpectedCourseCategories =
    {
        "Appetizer", "Soup", "Salad", "Main Course", "Dessert", "Beverage"
    };

    [Fact]
    public async Task SeedAsync_creates_roles_bootstrap_administrator_and_course_categories()
    {
        using var harness = CreateHarness(Environments.Development, "Admin@12345");

        await harness.Seeder.SeedAsync();

        (await harness.Context.Roles.Select(r => r.Name).ToListAsync())
            .Should().BeEquivalentTo(ExpectedRoles);

        var user = await harness.Context.Users.SingleAsync();
        user.Email.Should().Be("admin@salscatersuite.local");
        user.UserName.Should().Be("admin@salscatersuite.local");
        user.Firstname.Should().Be("System");
        user.Lastname.Should().Be("Administrator");
        user.IsActive.Should().BeTrue();
        user.EmailConfirmed.Should().BeTrue(
            "the system-provisioned bootstrap administrator is created with a confirmed email (DEC-033)");

        (await harness.UserManager.IsInRoleAsync(user, Roles.Administrator))
            .Should().BeTrue();

        (await harness.Context.CourseCategories.Select(c => c.CourseName).ToListAsync())
            .Should().BeEquivalentTo(ExpectedCourseCategories);
    }

    [Fact]
    public async Task SeedAsync_creates_no_fabricated_business_data()
    {
        using var harness = CreateHarness(Environments.Development, "Admin@12345");

        await harness.Seeder.SeedAsync();

        (await harness.Context.Customers.CountAsync()).Should().Be(0);
        (await harness.Context.Bookings.CountAsync()).Should().Be(0);
        (await harness.Context.Payments.CountAsync()).Should().Be(0);
        (await harness.Context.Refunds.CountAsync()).Should().Be(0);
        (await harness.Context.Menus.CountAsync()).Should().Be(0);
        (await harness.Context.Packages.CountAsync()).Should().Be(0);
        (await harness.Context.Reservations.CountAsync()).Should().Be(0);
        (await harness.Context.Areas.CountAsync()).Should().Be(0);
        (await harness.Context.AddonDetails.CountAsync()).Should().Be(0);
        (await harness.Context.Discounts.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task SeedAsync_is_idempotent_and_never_mutates_an_existing_account()
    {
        using var harness = CreateHarness(Environments.Development, "Admin@12345");

        await harness.Seeder.SeedAsync();

        var originalHash = (await harness.Context.Users.SingleAsync()).PasswordHash;
        var originalSecurityStamp = (await harness.Context.Users.SingleAsync()).SecurityStamp;

        await harness.Seeder.SeedAsync();

        (await harness.Context.Roles.CountAsync()).Should().Be(ExpectedRoles.Length);
        (await harness.Context.Users.CountAsync()).Should().Be(1);
        (await harness.Context.UserRoles.CountAsync()).Should().Be(1);
        (await harness.Context.CourseCategories.CountAsync()).Should().Be(ExpectedCourseCategories.Length);

        var user = await harness.Context.Users.SingleAsync();
        user.PasswordHash.Should().Be(originalHash, "an existing password must never be reset");
        user.SecurityStamp.Should().Be(originalSecurityStamp);
    }

    [Fact]
    public async Task SeedAsync_outside_development_without_configured_password_skips_the_user()
    {
        using var harness = CreateHarness(Environments.Production, password: null);

        await harness.Seeder.SeedAsync();

        // Roles and reference values are environment-independent and still seeded.
        (await harness.Context.Roles.CountAsync()).Should().Be(ExpectedRoles.Length);
        (await harness.Context.CourseCategories.CountAsync()).Should().Be(ExpectedCourseCategories.Length);

        // No credential is invented outside Development.
        (await harness.Context.Users.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task SeedAsync_in_development_without_configured_password_uses_flagged_fallback_credential()
    {
        var logger = new CapturingLogger<DatabaseSeeder>();
        using var harness = CreateHarness(Environments.Development, password: null, logger: logger);

        await harness.Seeder.SeedAsync();

        var user = await harness.Context.Users.SingleAsync();
        (await harness.UserManager.CheckPasswordAsync(user, "Admin@12345")).Should().BeTrue();

        logger.Messages.Should().Contain(
            m => m.Contains("DEVELOPMENT ONLY", StringComparison.Ordinal),
            "the well-known fallback password must always be flagged loudly");
    }

    [Fact]
    public async Task SeedAsync_uses_configured_values_for_the_bootstrap_administrator()
    {
        using var harness = CreateHarness(
            Environments.Development,
            password: "Sup3rSecret",
            seedValues: new Dictionary<string, string?>
            {
                ["Seed:Admin:Email"] = "owner@example.test",
                ["Seed:Admin:UserName"] = "owner",
                ["Seed:Admin:FirstName"] = "Ada",
                ["Seed:Admin:LastName"] = "Lovelace"
            });

        await harness.Seeder.SeedAsync();

        var user = await harness.Context.Users.SingleAsync();
        user.Email.Should().Be("owner@example.test");
        user.UserName.Should().Be("owner");
        user.Firstname.Should().Be("Ada");
        user.Lastname.Should().Be("Lovelace");
        (await harness.UserManager.CheckPasswordAsync(user, "Sup3rSecret")).Should().BeTrue();
    }

    // ── Test harness ────────────────────────────────────────────────────────

    private static Harness CreateHarness(
        string environmentName,
        string? password,
        IDictionary<string, string?>? seedValues = null,
        ILogger<DatabaseSeeder>? logger = null)
    {
        var context = new SalsCaterSuiteDbContext(
            new DbContextOptionsBuilder<SalsCaterSuiteDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options);

        var identityOptions = Options.Create(new IdentityOptions
        {
            Password = new PasswordOptions
            {
                RequireDigit = true,
                RequiredLength = 8,
                RequireUppercase = false,
                RequireNonAlphanumeric = false
            },
            User = new UserOptions { RequireUniqueEmail = false }
        });

        var normalizer = new UpperInvariantLookupNormalizer();

        var userManager = new UserManager<ApplicationUser>(
            new UserStore<ApplicationUser, ApplicationRole, SalsCaterSuiteDbContext>(context),
            identityOptions,
            new LegacyPasswordHasher<ApplicationUser>(),
            new[] { new UserValidator<ApplicationUser>() },
            new IPasswordValidator<ApplicationUser>[] { new PasswordValidator<ApplicationUser>() },
            normalizer,
            new IdentityErrorDescriber(),
            new Mock<IServiceProvider>().Object,
            NullLogger<UserManager<ApplicationUser>>.Instance);

        var roleManager = new RoleManager<ApplicationRole>(
            new RoleStore<ApplicationRole, SalsCaterSuiteDbContext>(context),
            new[] { new RoleValidator<ApplicationRole>() },
            normalizer,
            new IdentityErrorDescriber(),
            NullLogger<RoleManager<ApplicationRole>>.Instance);

        var values = new Dictionary<string, string?>(seedValues ?? new Dictionary<string, string?>());
        if (password is not null)
        {
            values["Seed:Admin:Password"] = password;
        }

        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();

        var environment = new Mock<IHostEnvironment>();
        environment.SetupGet(e => e.EnvironmentName).Returns(environmentName);
        environment.SetupGet(e => e.ApplicationName).Returns("SalsCaterSuite.UnitTests");

        var seeder = new DatabaseSeeder(
            new CourseCategoryRepository(context),
            new DatabaseMigrator(context),
            roleManager,
            userManager,
            configuration,
            environment.Object,
            logger ?? NullLogger<DatabaseSeeder>.Instance);

        return new Harness(context, seeder, userManager);
    }

    private sealed record Harness(
        SalsCaterSuiteDbContext Context,
        DatabaseSeeder Seeder,
        UserManager<ApplicationUser> UserManager) : IDisposable
    {
        public void Dispose() => Context.Dispose();
    }

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public List<string> Messages { get; } = new();

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
            => Messages.Add(formatter(state, exception));
    }
}
