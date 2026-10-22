namespace SalsCaterSuite.UnitTests.Api.Logging;

using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Serilog;
using Serilog.Context;
using SalsCaterSuite.WebApi.Logging;
using Xunit;

/// <summary>
/// Verifies the destination contract of the operational log (specification §5.1, AC-OPS-13,
/// AC-OPS-15, BR-23). The sink list is configuration, so moving the log to a file, to the console
/// or to both is an <c>appsettings</c> edit and never a rebuild.
/// </summary>
public class SerilogLoggingConfigurationTests
{
    private const string CompactJsonFormatter =
        "Serilog.Formatting.Compact.RenderedCompactJsonFormatter, Serilog.Formatting.Compact";

    [Fact]
    public void HasConfiguredSinks_WhenTheWriteToSectionIsAbsent_ReturnsFalse()
    {
        var configuration = new ConfigurationBuilder().Build();

        SerilogLoggingConfiguration.HasConfiguredSinks(configuration).Should().BeFalse();
    }

    [Fact]
    public void HasConfiguredSinks_WhenTheWriteToSectionIsEmpty_ReturnsFalse()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Serilog:MinimumLevel:Default"] = "Information" })
            .Build();

        SerilogLoggingConfiguration.HasConfiguredSinks(configuration).Should().BeFalse();
    }

    [Fact]
    public void HasConfiguredSinks_WhenTheWriteToSectionIsPresent_ReturnsTrue()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Serilog:WriteTo:0:Name"] = "Console" })
            .Build();

        SerilogLoggingConfiguration.HasConfiguredSinks(configuration).Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void HasConfiguredSinks_WhenAChildHasABlankName_ReturnsFalse(string blankName)
    {
        // A child whose Name is absent or blank does not count: Serilog's configuration reader
        // accepts such an element without throwing and produces a SINKLESS logger, which silently
        // discards every line (AC-OPS-13 / risk R4).
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Serilog:WriteTo:0:Name"] = blankName })
            .Build();

        SerilogLoggingConfiguration.HasConfiguredSinks(configuration).Should().BeFalse();
    }

    [Fact]
    public void HasConfiguredSinks_WhenAChildHasNoNameAtAll_ReturnsFalse()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Serilog:WriteTo:0:Args:path"] = "logs/x.log" })
            .Build();

        SerilogLoggingConfiguration.HasConfiguredSinks(configuration).Should().BeFalse();
    }

    [Fact]
    public void Configure_WhenEveryChildHasABlankName_FallsBackToTheConsoleSink()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Serilog:WriteTo:0:Name"] = "  " })
            .Build();

        var (output, _) = CaptureConsole(() =>
        {
            using var logger = SerilogLoggingConfiguration
                .Configure(configuration, "Testing", new LoggerConfiguration())
                .CreateLogger();

            logger.Information("the safety net is alive");
        });

        output.Should().Contain("the safety net is alive");
    }

    [Fact]
    public void Configure_WhenTheWriteToSectionIsEmpty_FallsBackToTheConsoleSink()
    {
        // AC-OPS-13 / risk R4 — a misconfigured deployment must never produce a logger with no
        // sink, which would silently discard every log line.
        var configuration = new ConfigurationBuilder().Build();

        var (output, _) = CaptureConsole(() =>
        {
            using var logger = SerilogLoggingConfiguration
                .Configure(configuration, "Testing", new LoggerConfiguration())
                .CreateLogger();

            logger.Information("the safety net is alive");
        });

        output.Should().Contain("the safety net is alive");
        output.Trim().Should().StartWith("{", "the console fallback writes one JSON object per line");
    }

    [Fact]
    public void Configure_WhenTheWriteToSectionIsPresent_DoesNotAddTheConsoleFallback()
    {
        var directory = CreateTemporaryDirectory();

        try
        {
            var configuration = BuildFileSinkConfiguration(directory);

            var (output, _) = CaptureConsole(() =>
            {
                using var logger = SerilogLoggingConfiguration
                    .Configure(configuration, "Testing", new LoggerConfiguration())
                    .CreateLogger();

                logger.Information("written to the file, not to the console");
            });

            output.Should().NotContain("written to the file, not to the console");
            ReadLogFiles(directory).Should().ContainSingle()
                .Which.Should().Contain("written to the file, not to the console");
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    [Fact]
    public void Configure_EnrichesEveryEventWithTheApplicationAndTheEnvironment()
    {
        var directory = CreateTemporaryDirectory();

        try
        {
            var configuration = BuildFileSinkConfiguration(directory);

            using (var logger = SerilogLoggingConfiguration
                .Configure(configuration, "Testing", new LoggerConfiguration())
                .CreateLogger())
            {
                logger.Information("an event");
            }

            var line = ReadLogFiles(directory).Should().ContainSingle().Which;
            var parsed = Parse(line);

            parsed.GetProperty("Application").GetString().Should().Be("SalsCaterSuite.Api");
            parsed.GetProperty("Environment").GetString().Should().Be("Testing");
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    [Fact]
    public void Configure_EnrichesEveryEventFromTheLogContext()
    {
        // AC-OPS-11 — the correlation id is only carried by the other log lines of a request
        // when the log context is enabled.
        var directory = CreateTemporaryDirectory();

        try
        {
            var configuration = BuildFileSinkConfiguration(directory);

            using (var logger = SerilogLoggingConfiguration
                .Configure(configuration, "Testing", new LoggerConfiguration())
                .CreateLogger())
            {
                using (LogContext.PushProperty("TraceId", "trace-42"))
                {
                    logger.Information("an event inside a request");
                }
            }

            var line = ReadLogFiles(directory).Should().ContainSingle().Which;
            Parse(line).GetProperty("TraceId").GetString().Should().Be("trace-42");
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    [Fact]
    public void Configure_WritesOneJsonObjectPerLineToTheRollingFile()
    {
        // AC-OPS-15 / BR-23.3 — the Datadog Agent parses one JSON object per line.
        var directory = CreateTemporaryDirectory();

        try
        {
            var configuration = BuildFileSinkConfiguration(directory);

            using (var logger = SerilogLoggingConfiguration
                .Configure(configuration, "Testing", new LoggerConfiguration())
                .CreateLogger())
            {
                logger.Information("first line");
                logger.Information("second line");
            }

            var lines = ReadLogFiles(directory);

            lines.Should().HaveCount(2);
            lines.Should().OnlyContain(line => line.TrimStart().StartsWith("{") && line.TrimEnd().EndsWith("}"));

            foreach (var line in lines)
            {
                var parsed = Parse(line);
                parsed.TryGetProperty("@t", out _).Should().BeTrue("every line carries a timestamp");
            }
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    [Fact]
    public void Configure_RollsTheFileByDay()
    {
        var directory = CreateTemporaryDirectory();

        try
        {
            var configuration = BuildFileSinkConfiguration(directory);

            using (var logger = SerilogLoggingConfiguration
                .Configure(configuration, "Testing", new LoggerConfiguration())
                .CreateLogger())
            {
                logger.Information("an event");
            }

            var file = Directory.GetFiles(directory, "*.log").Should().ContainSingle().Subject;
            Path.GetFileName(file).Should().Match("salscatersuite-*", "the file name carries the rolling date");
            Path.GetFileName(file).Should().NotBe("salscatersuite-.log");
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    [Fact]
    public void ShippedAppSettings_DeclaresTheRollingFileSinkWithTheRetentionPolicy()
    {
        // BR-23.2 — one file per day, 31 files retained, a 10 MB size-based roll, JSON lines.
        var settings = RequestLoggingOptionsTests.ReadShippedSettings(RequestLoggingOptionsTests.ShippedAppSettingsPath);
        var writeTo = settings.GetProperty("Serilog").GetProperty("WriteTo");

        writeTo.GetArrayLength().Should().BeGreaterThanOrEqualTo(2);

        var fileSink = writeTo.EnumerateArray()
            .First(sink => sink.GetProperty("Name").GetString() == "File")
            .GetProperty("Args");

        fileSink.GetProperty("path").GetString().Should().StartWith("logs/");
        fileSink.GetProperty("rollingInterval").GetString().Should().Be("Day");
        fileSink.GetProperty("retainedFileCountLimit").GetInt32().Should().Be(31);
        fileSink.GetProperty("fileSizeLimitBytes").GetInt64().Should().Be(10 * 1024 * 1024);
        fileSink.GetProperty("rollOnFileSizeLimit").GetBoolean().Should().BeTrue();
        fileSink.GetProperty("formatter").GetString().Should().Be(CompactJsonFormatter);
    }

    [Fact]
    public void ShippedAppSettings_DeclaresTheConsoleSinkWithTheJsonFormatter()
    {
        var settings = RequestLoggingOptionsTests.ReadShippedSettings(RequestLoggingOptionsTests.ShippedAppSettingsPath);
        var writeTo = settings.GetProperty("Serilog").GetProperty("WriteTo");

        var consoleSink = writeTo.EnumerateArray()
            .First(sink => sink.GetProperty("Name").GetString() == "Console")
            .GetProperty("Args");

        consoleSink.GetProperty("formatter").GetString().Should().Be(CompactJsonFormatter);
    }

    [Fact]
    public void ShippedAppSettings_DeclaresTheMinimumLevelOverrides()
    {
        var settings = RequestLoggingOptionsTests.ReadShippedSettings(RequestLoggingOptionsTests.ShippedAppSettingsPath);
        var minimumLevel = settings.GetProperty("Serilog").GetProperty("MinimumLevel");

        minimumLevel.GetProperty("Default").GetString().Should().Be("Information");
        minimumLevel.GetProperty("Override").GetProperty("Microsoft.AspNetCore").GetString().Should().Be("Warning");
        minimumLevel.GetProperty("Override").GetProperty("Microsoft.EntityFrameworkCore").GetString().Should().Be("Warning");
    }

    [Fact]
    public void ShippedDevelopmentAppSettings_MirrorsThePreSerilogLevels()
    {
        // The Development file must mirror the levels the legacy "Logging:LogLevel" subtree had
        // before Serilog took over, so the Development console's verbosity does not change
        // silently (specification §5.1).
        var settings = RequestLoggingOptionsTests.ReadShippedSettings(RequestLoggingOptionsTests.ShippedDevelopmentAppSettingsPath);
        var minimumLevel = settings.GetProperty("Serilog").GetProperty("MinimumLevel");

        minimumLevel.GetProperty("Default").GetString().Should().Be("Debug");
        minimumLevel.GetProperty("Override").GetProperty("Microsoft.AspNetCore").GetString().Should().Be("Information");
        minimumLevel.GetProperty("Override").GetProperty("Microsoft.EntityFrameworkCore").GetString().Should().Be("Information");
    }

    [Fact]
    public void ShippedDevelopmentAppSettings_KeepsAspNetCoreAtInformationAndDoesNotRepeatTheSinkList()
    {
        var settings = RequestLoggingOptionsTests.ReadShippedSettings(RequestLoggingOptionsTests.ShippedDevelopmentAppSettingsPath);
        var serilog = settings.GetProperty("Serilog");

        serilog.GetProperty("MinimumLevel").GetProperty("Override")
            .GetProperty("Microsoft.AspNetCore").GetString().Should().Be("Information");

        serilog.TryGetProperty("WriteTo", out _).Should().BeFalse(
            "the sink list is declared once, in the base configuration");
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private static IConfiguration BuildFileSinkConfiguration(string directory) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Serilog:MinimumLevel:Default"] = "Information",
                ["Serilog:WriteTo:0:Name"] = "File",
                ["Serilog:WriteTo:0:Args:path"] = Path.Combine(directory, "salscatersuite-.log"),
                ["Serilog:WriteTo:0:Args:rollingInterval"] = "Day",
                ["Serilog:WriteTo:0:Args:retainedFileCountLimit"] = "31",
                ["Serilog:WriteTo:0:Args:fileSizeLimitBytes"] = "10485760",
                ["Serilog:WriteTo:0:Args:rollOnFileSizeLimit"] = "true",
                ["Serilog:WriteTo:0:Args:formatter"] = CompactJsonFormatter
            })
            .Build();

    private static IReadOnlyList<string> ReadLogFiles(string directory) =>
        Directory.GetFiles(directory, "*.log")
            .SelectMany(File.ReadAllLines)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToArray();

    private static JsonElement Parse(string line)
    {
        using var document = JsonDocument.Parse(line);
        return document.RootElement.Clone();
    }

    private static string CreateTemporaryDirectory()
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            "salscatersuite-logging-tests",
            Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(directory);

        return directory;
    }

    private static void DeleteDirectory(string directory)
    {
        try
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
        catch (IOException)
        {
            // Evidence cleanup must never fail a test run.
        }
    }

    private static (string Output, string Error) CaptureConsole(Action action)
    {
        var originalOut = Console.Out;
        var originalError = Console.Error;
        var capturedOut = new StringWriter();
        var capturedError = new StringWriter();

        try
        {
            Console.SetOut(capturedOut);
            Console.SetError(capturedError);

            action();
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalError);
        }

        return (capturedOut.ToString(), capturedError.ToString());
    }
}
