using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.TestHost;

namespace SalsCaterSuite.IntegrationTests.Infrastructure;

/// <summary>
/// Test host for the Sal's CaterSuite Web API (DEC-019).
///
/// Design rules:
/// <list type="bullet">
/// <item>The environment is <c>Testing</c>, so the base <c>appsettings.json</c> applies:
/// <c>Database:ApplyMigrationsOnStartup</c> and <c>Database:SeedOnStartup</c> are both false and
/// therefore no automatic migration or seeding runs while the host starts. The fixture migrates
/// and seeds explicitly. Development must never be used, because `appsettings.Development.json`
/// targets the Developer LocalDB database `SalsCaterSuiteDb` and enables auto-migrate/auto-seed.</item>
/// <item>The connection string is replaced with a per-run disposable database named
/// <c>SalsCaterSuiteDb_Integration_{Guid:N}</c> on LocalDB <c>(localdb)\mssqllocaldb</c> — never
/// the developer database, never a shared/remote server (DEC-020).</item>
/// <item>The bootstrap administrator credentials are supplied for the Testing environment only so
/// the seeder provisions the account under test (DEC-018 / DEC-033). They are test-only values.</item>
/// <item>The rolling log file of the operational logger (REQ-OPS-01) is redirected to a
/// <b>per-run temporary directory</b>, so a test run never writes into the repository's
/// <c>logs/</c> folder and never reads a previous run's file.</item>
/// </list>
/// </summary>
public sealed class IntegrationTestWebApplicationFactory : WebApplicationFactory<Program>
{
    public const string LocalDbServer = @"(localdb)\mssqllocaldb";

    public const string AdminEmail = "admin@salscatersuite.local";
    public const string AdminUserName = "admin@salscatersuite.local";
    public const string AdminPassword = "Admin@12345";

    public const string DatabasePrefix = "SalsCaterSuiteDb_Integration_";

    /// <summary>The compact JSON formatter both sinks use (BR-23.3 — the Datadog Agent parses it).</summary>
    private const string CompactJsonFormatter =
        "Serilog.Formatting.Compact.RenderedCompactJsonFormatter, Serilog.Formatting.Compact";

    public IntegrationTestWebApplicationFactory(string? databaseName = null)
    {
        DatabaseName = databaseName ?? $"{DatabasePrefix}{Guid.NewGuid():N}";
        ConnectionString = BuildConnectionString(DatabaseName);

        LogDirectory = Path.Combine(
            Path.GetTempPath(),
            "salscatersuite-integration-logs",
            Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(LogDirectory);
    }

    public string DatabaseName { get; }

    public string ConnectionString { get; }

    /// <summary>The per-run directory that receives the rolling log file.</summary>
    public string LogDirectory { get; }

    /// <summary>The path template of the rolling log file (the rolling date is appended).</summary>
    public string LogFilePath => Path.Combine(LogDirectory, "salscatersuite-.log");

    public static string BuildConnectionString(string databaseName) =>
        $"Server={LocalDbServer};Database={databaseName};Trusted_Connection=True;MultipleActiveResultSets=true";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Testing (not Development): the base appsettings.json flags apply, so the host never
        // migrates or seeds on startup and the Development connection string is never used.
        builder.UseEnvironment("Testing");

        // Host-level settings, applied before the entry point composes its services.
        builder.UseSetting("ConnectionStrings:SalsCaterSuiteDb", ConnectionString);
        builder.UseSetting("Database:ApplyMigrationsOnStartup", "false");
        builder.UseSetting("Database:SeedOnStartup", "false");
        // BG-04 must not mutate seeded past events while a test is asserting them (DEC-178).
        builder.UseSetting("BookingLifecycle:Enabled", "false");
        // Test-run noise control only: EF command logging is not needed for assertions. Nothing
        // about the API's own behaviour depends on this. Serilog owns the logging pipeline now
        // (DEC-092), so the override moves from the legacy "Logging:LogLevel" subtree to the
        // Serilog minimum-level overrides.
        builder.UseSetting("Serilog:MinimumLevel:Override:Microsoft.EntityFrameworkCore.Database.Command", "Warning");

        // The rolling log file goes to the per-run temporary directory, never to the repository.
        //
        // IMPORTANT: the "Serilog:WriteTo:*" keys are set in the APPLICATION configuration only
        // (the in-memory collection below) and NOT through UseSetting. A host setting with a
        // nested key ("Serilog:WriteTo:0:Name") materialises the whole path into the host
        // configuration with EMPTY intermediate values, so "Serilog:WriteTo:0" resolves to an
        // empty string. Serilog's configuration reader then treats every "WriteTo" element as a
        // plain sink name instead of a section, no sink is configured at all, and the log file is
        // never created. The application configuration — the one Serilog actually reads — carries
        // flat keys only, so it is unaffected.

        // Application-level configuration, authoritative for anything that resolves
        // IConfiguration (for example the database seeder reading Seed:Admin).
        builder.ConfigureAppConfiguration((_, configuration) =>
        {            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:SalsCaterSuiteDb"] = ConnectionString,
                ["Database:ApplyMigrationsOnStartup"] = "false",
                ["Database:SeedOnStartup"] = "false",
                ["BookingLifecycle:Enabled"] = "false",
                ["Serilog:MinimumLevel:Override:Microsoft.EntityFrameworkCore.Database.Command"] = "Warning",

                // The host setting above materialises the whole "Serilog:MinimumLevel:Override:..."
                // path with EMPTY intermediate values, so the scope
                // "Microsoft.EntityFrameworkCore.Database" would otherwise resolve to an empty
                // level. It is pinned to the same level explicitly.
                ["Serilog:MinimumLevel:Override:Microsoft.EntityFrameworkCore.Database"] = "Warning",

                ["Serilog:WriteTo:0:Name"] = "Console",
                ["Serilog:WriteTo:0:Args:formatter"] = CompactJsonFormatter,
                ["Serilog:WriteTo:1:Name"] = "File",
                ["Serilog:WriteTo:1:Args:path"] = LogFilePath,
                ["Serilog:WriteTo:1:Args:rollingInterval"] = "Day",
                ["Serilog:WriteTo:1:Args:retainedFileCountLimit"] = "31",
                ["Serilog:WriteTo:1:Args:fileSizeLimitBytes"] = "10485760",
                ["Serilog:WriteTo:1:Args:rollOnFileSizeLimit"] = "true",
                ["Serilog:WriteTo:1:Args:formatter"] = CompactJsonFormatter,

                ["Seed:Admin:Email"] = AdminEmail,
                ["Seed:Admin:UserName"] = AdminUserName,
                ["Seed:Admin:Password"] = AdminPassword,
                ["Seed:Admin:FirstName"] = "System",
                ["Seed:Admin:LastName"] = "Administrator",
            });
        });

        // The test-only controller of the operational-logging feature (specification §7). It is
        // registered through a dedicated ApplicationPart, so NO production route is added: the
        // controller lives in the integration-test assembly and is only ever discovered by the
        // test host.
        builder.ConfigureTestServices(services =>
        {
            services.AddControllers()
                .AddApplicationPart(typeof(TestOperationalLoggingController).Assembly);
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (!disposing)
        {
            return;
        }

        try
        {
            if (Directory.Exists(LogDirectory))
            {
                Directory.Delete(LogDirectory, recursive: true);
            }
        }
        catch (IOException)
        {
            // Evidence cleanup must never fail a test run.
        }
    }
}
