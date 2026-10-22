namespace SalsCaterSuite.UnitTests.Api.Logging;

using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SalsCaterSuite.WebApi.Logging;
using Xunit;

/// <summary>
/// Verifies the configuration contract of the operational request logger (specification §5.2).
/// The options are <b>configuration, not code</b> (BR-21.3), so the defaults and the shipped
/// <c>appsettings.json</c> are asserted to agree.
/// </summary>
public class RequestLoggingOptionsTests
{
    /// <summary>The shipped configuration files, copied next to the test assembly.</summary>
    internal static string ShippedAppSettingsPath => Path.Combine(AppContext.BaseDirectory, "appsettings.json");

    internal static string ShippedDevelopmentAppSettingsPath =>
        Path.Combine(AppContext.BaseDirectory, "appsettings.Development.json");

    /// <summary>Reads a shipped settings file, tolerating the JSON comments the host allows.</summary>
    internal static JsonElement ReadShippedSettings(string path)
    {
        var json = File.ReadAllText(path);

        using var document = JsonDocument.Parse(
            json,
            new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });

        return document.RootElement.Clone();
    }

    [Fact]
    public void Defaults_MatchTheSpecification()
    {
        var options = new RequestLoggingOptions();

        options.Enabled.Should().BeTrue("the master switch is on by default (specification §5.2)");
        options.LogRequestBody.Should().BeTrue();
        options.LogResponseBody.Should().BeTrue();
        options.MaxBodyLengthBytes.Should().Be(8192, "BR-22.1 fixes the capture cap at 8 192 bytes");
        options.ClientErrorLevel.Should().Be(Serilog.Events.LogEventLevel.Warning);
        options.ServerErrorLevel.Should().Be(Serilog.Events.LogEventLevel.Error);
        options.ExcludedPathPrefixes.Should().BeEmpty("every endpoint is logged by default");
    }

    [Fact]
    public void Defaults_CarryTheBr21SensitiveNameSets()
    {
        var options = new RequestLoggingOptions();

        options.EffectiveSensitiveFieldNames.Should().BeEquivalentTo(RequestLoggingOptions.DefaultSensitiveFieldNames);
        options.EffectiveSensitiveQueryParameterNames.Should().BeEquivalentTo(RequestLoggingOptions.DefaultSensitiveQueryParameterNames);

        // BR-21.4 — the query set is the body set plus access_token (the SignalR bearer) and
        // search (a customer search term that matches a name, a company and a contact number).
        options.EffectiveSensitiveQueryParameterNames.Should().Contain("access_token");
        options.EffectiveSensitiveQueryParameterNames.Should().Contain("search");
        options.EffectiveSensitiveQueryParameterNames.Should().Contain("password");
        options.EffectiveSensitiveQueryParameterNames.Should().Contain("email");
    }

    [Fact]
    public void Defaults_NameExactlyThePropertiesThatExistOnTheAsBuiltModel()
    {
        // DEC-096 — the set is verified against the as-built DTOs. A name that no property carries
        // and that is not a documented forward-looking name must not be in it.
        var expected = new[]
        {
            // Secrets — never negotiable.
            "password", "passwordconfirmation", "newpassword", "confirmpassword", "token",
            "accesstoken", "refreshtoken", "idtoken", "jwt", "secret", "secretkey", "apikey",
            "api_key", "authorization", "clientsecret", "signature", "privatekey", "connectionstring",

            // Account identifiers.
            "username", "userName", "createdbyuser",

            // Customer contact identifiers.
            "address", "contact", "contact1", "contact2", "customercontact",

            // Forward-looking — no property in the current model carries an e-mail address or a TIN.
            "email", "emailaddress", "tin"
        };

        var options = new RequestLoggingOptions();

        options.EffectiveSensitiveFieldNames.Should().BeEquivalentTo(expected);
        options.EffectiveSensitiveQueryParameterNames
            .Should().BeEquivalentTo(expected.Append("access_token").Append("search"));
    }

    [Fact]
    public void Defaults_DoNotRedactCustomerNamesOrAmounts()
    {
        // BR-21.8 / DEC-093 — names and amounts stay visible on purpose.
        var options = new RequestLoggingOptions();

        options.EffectiveSensitiveFieldNames.Should().NotContain("name");
        options.EffectiveSensitiveFieldNames.Should().NotContain("customername");
        options.EffectiveSensitiveFieldNames.Should().NotContain("amount");
        options.EffectiveSensitiveFieldNames.Should().NotContain("amountdue");
    }

    [Fact]
    public void ConfigurationBinding_BindsTheRequestLoggingSection()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Logging:RequestLogging:Enabled"] = "false",
                ["Logging:RequestLogging:LogRequestBody"] = "false",
                ["Logging:RequestLogging:LogResponseBody"] = "false",
                ["Logging:RequestLogging:MaxBodyLengthBytes"] = "256",
                ["Logging:RequestLogging:ClientErrorLevel"] = "Error",
                ["Logging:RequestLogging:ServerErrorLevel"] = "Fatal",
                ["Logging:RequestLogging:SensitiveFieldNames:0"] = "password",
                ["Logging:RequestLogging:SensitiveQueryParameterNames:0"] = "password",
                ["Logging:RequestLogging:ExcludedPathPrefixes:0"] = "/health"
            })
            .Build();

        var options = configuration
            .GetSection(RequestLoggingOptions.SectionName)
            .Get<RequestLoggingOptions>();

        options.Should().NotBeNull();
        options!.Enabled.Should().BeFalse();
        options.LogRequestBody.Should().BeFalse();
        options.LogResponseBody.Should().BeFalse();
        options.MaxBodyLengthBytes.Should().Be(256);
        options.ClientErrorLevel.Should().Be(Serilog.Events.LogEventLevel.Error);
        options.ServerErrorLevel.Should().Be(Serilog.Events.LogEventLevel.Fatal);

        // The configured set REPLACES the BR-21 baseline — a name can genuinely be removed
        // without a rebuild (BR-21.3).
        options.SensitiveFieldNames.Should().Equal("password");
        options.SensitiveQueryParameterNames.Should().Equal("password");
        options.EffectiveSensitiveFieldNames.Should().Equal("password");
        options.ExcludedPathPrefixes.Should().Equal("/health");
    }

    [Fact]
    public void ConfigurationBinding_WithoutTheSection_KeepsTheDefaults()
    {
        var configuration = new ConfigurationBuilder().Build();

        var options = configuration
            .GetSection(RequestLoggingOptions.SectionName)
            .Get<RequestLoggingOptions>() ?? new RequestLoggingOptions();

        options.Enabled.Should().BeTrue();
        options.MaxBodyLengthBytes.Should().Be(8192);
        options.EffectiveSensitiveFieldNames.Should().BeEquivalentTo(RequestLoggingOptions.DefaultSensitiveFieldNames);
    }

    [Fact]
    public void MasterSwitch_DefaultsToEnabledAndCanBeDisabledByConfiguration()
    {
        // AC-OPS-14 — the master switch is the only way to remove the record entirely.
        new RequestLoggingOptions().Enabled.Should().BeTrue();

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Logging:RequestLogging:Enabled"] = "false"
            })
            .Build();

        var options = configuration
            .GetSection(RequestLoggingOptions.SectionName)
            .Get<RequestLoggingOptions>();

        options!.Enabled.Should().BeFalse();
    }

    [Fact]
    public void Options_CanBeResolvedThroughTheOptionsInfrastructure()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Logging:RequestLogging:MaxBodyLengthBytes"] = "1024"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddOptions<RequestLoggingOptions>()
            .Configure(options => configuration.GetSection(RequestLoggingOptions.SectionName).Bind(options));

        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<IOptions<RequestLoggingOptions>>()
            .Value.MaxBodyLengthBytes.Should().Be(1024);
    }

    [Fact]
    public void ShippedAppSettings_DeclaresTheSameSensitiveSetsAsTheDefaults()
    {
        // The configuration and the code default must never drift: an operator who edits the
        // shipped file must be editing the set that is actually in force.
        var settings = ReadShippedSettings(ShippedAppSettingsPath);
        var requestLogging = settings.GetProperty("Logging").GetProperty("RequestLogging");

        var sensitiveFieldNames = requestLogging.GetProperty("SensitiveFieldNames")
            .EnumerateArray().Select(value => value.GetString()).ToArray();
        var sensitiveQueryParameterNames = requestLogging.GetProperty("SensitiveQueryParameterNames")
            .EnumerateArray().Select(value => value.GetString()).ToArray();

        sensitiveFieldNames.Should().BeEquivalentTo(RequestLoggingOptions.DefaultSensitiveFieldNames);
        sensitiveQueryParameterNames.Should().BeEquivalentTo(RequestLoggingOptions.DefaultSensitiveQueryParameterNames);
    }

    [Fact]
    public void ShippedAppSettings_DeclaresTheSpecificationDefaults()
    {
        var settings = ReadShippedSettings(ShippedAppSettingsPath);
        var requestLogging = settings.GetProperty("Logging").GetProperty("RequestLogging");

        requestLogging.GetProperty("Enabled").GetBoolean().Should().BeTrue();
        requestLogging.GetProperty("LogRequestBody").GetBoolean().Should().BeTrue();
        requestLogging.GetProperty("LogResponseBody").GetBoolean().Should().BeTrue();
        requestLogging.GetProperty("MaxBodyLengthBytes").GetInt32().Should().Be(8192);
        requestLogging.GetProperty("ClientErrorLevel").GetString().Should().Be("Warning");
        requestLogging.GetProperty("ServerErrorLevel").GetString().Should().Be("Error");
        requestLogging.GetProperty("ExcludedPathPrefixes").GetArrayLength().Should().Be(0);
    }

    [Fact]
    public void ShippedDevelopmentAppSettings_DeclaresTheSameRequestLoggingSection()
    {
        var settings = ReadShippedSettings(ShippedDevelopmentAppSettingsPath);
        var requestLogging = settings.GetProperty("Logging").GetProperty("RequestLogging");

        requestLogging.GetProperty("MaxBodyLengthBytes").GetInt32().Should().Be(8192);
        requestLogging.GetProperty("ClientErrorLevel").GetString().Should().Be("Warning");
        requestLogging.GetProperty("ServerErrorLevel").GetString().Should().Be("Error");

        requestLogging.GetProperty("SensitiveFieldNames")
            .EnumerateArray().Select(value => value.GetString())
            .Should().BeEquivalentTo(RequestLoggingOptions.DefaultSensitiveFieldNames);
    }

    [Fact]
    public void ShippedAppSettings_KeepsTheExcludedPathPrefixesEmpty()
    {
        // The base configuration logs EVERY endpoint — the requirement is a record for every one.
        var settings = ReadShippedSettings(ShippedAppSettingsPath);
        var requestLogging = settings.GetProperty("Logging").GetProperty("RequestLogging");

        requestLogging.GetProperty("ExcludedPathPrefixes").GetArrayLength().Should().Be(0);
    }

    [Fact]
    public void ShippedDevelopmentAppSettings_ExcludesTheSwaggerUi()
    {
        // The Swagger UI page is a text/html document, so BR-21.7 treats it as textual and it
        // would be captured on every page load. It is excluded in Development only.
        var settings = ReadShippedSettings(ShippedDevelopmentAppSettingsPath);
        var requestLogging = settings.GetProperty("Logging").GetProperty("RequestLogging");

        requestLogging.GetProperty("ExcludedPathPrefixes")
            .EnumerateArray().Select(value => value.GetString())
            .Should().Equal("/swagger");
    }

    [Fact]
    public void ShippedAppSettings_NoLongerCarriesTheLegacyLogLevelSection()
    {
        // Serilog owns the pipeline now, so the legacy "Logging:LogLevel" subtree is removed.
        var settings = ReadShippedSettings(ShippedAppSettingsPath);
        var logging = settings.GetProperty("Logging");

        logging.TryGetProperty("LogLevel", out _).Should().BeFalse(
            "the legacy Logging:LogLevel subtree is inert once Serilog owns the pipeline");

        var developmentSettings = ReadShippedSettings(ShippedDevelopmentAppSettingsPath);
        developmentSettings.GetProperty("Logging").TryGetProperty("LogLevel", out _).Should().BeFalse();
    }
}
