namespace SalsCaterSuite.WebApi.Logging;

using Serilog;
using Serilog.Formatting.Compact;

/// <summary>
/// Builds the Serilog pipeline for the API host (REQ-OPS-01, specification §5.1).
/// <para>
/// The destination list is <b>configuration</b> (<c>Serilog:WriteTo</c>), so moving the log to a
/// file, to the console, to both, or to a platform such as DataDog is an <c>appsettings</c> edit
/// and never a rebuild (BR-23.1). The DataDog route is the Datadog Agent tailing the JSON log
/// file (DEC-095), so this project carries no DataDog package at all.
/// </para>
/// <para>
/// The class is deliberately static and side-effect free so the console safety net (AC-OPS-13)
/// can be asserted by a unit test.
/// </para>
/// </summary>
public static class SerilogLoggingConfiguration
{
    /// <summary>The configuration section that owns the sink list (specification §5.1).</summary>
    public const string SectionName = "Serilog";

    /// <summary>The <c>Application</c> property written on every event.</summary>
    public const string ApplicationName = "SalsCaterSuite.Api";

    /// <summary>
    /// Configures the logger: reads the <c>Serilog</c> section, enriches every event with the
    /// log context, the application name and the environment, and applies the console safety net.
    /// </summary>
    /// <param name="configuration">The host configuration (the <c>Serilog</c> section is read from it).</param>
    /// <param name="environmentName">The hosting environment name, written on every event.</param>
    /// <param name="loggerConfiguration">The configuration built by the host builder.</param>
    public static LoggerConfiguration Configure(
        IConfiguration configuration,
        string environmentName,
        LoggerConfiguration loggerConfiguration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(loggerConfiguration);

        loggerConfiguration
            .ReadFrom.Configuration(configuration)
            // AC-OPS-11 — the correlation id is pushed into the log context for the duration of
            // the request, so every other line raised while the request is handled carries it.
            .Enrich.FromLogContext()
            .Enrich.WithProperty("Application", ApplicationName)
            .Enrich.WithProperty("Environment", environmentName);

        // Safety net (AC-OPS-13 / risk R4): a misconfigured "Serilog" section must never produce a
        // logger with no sink, which would silently discard every line.
        if (!HasConfiguredSinks(configuration))
        {
            loggerConfiguration.WriteTo.Console(new RenderedCompactJsonFormatter());
        }

        return loggerConfiguration;
    }

    /// <summary>
    /// Whether the configuration declares at least one <b>usable</b> sink in
    /// <c>Serilog:WriteTo</c>. An absent or empty list means the sink list is misconfigured, which
    /// triggers the console safety net.
    /// <para>
    /// A child whose <c>Name</c> is absent or blank does <b>not</b> count: Serilog's configuration
    /// reader accepts such an element without throwing and produces a <b>sinkless</b> logger, which
    /// silently discards every line (AC-OPS-13 / risk R4). Only a child with a non-empty
    /// <c>Name</c> is a configured sink.
    /// </para>
    /// </summary>
    public static bool HasConfiguredSinks(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        return configuration
            .GetSection($"{SectionName}:WriteTo")
            .GetChildren()
            .Any(child => !string.IsNullOrWhiteSpace(child["Name"]));
    }
}
