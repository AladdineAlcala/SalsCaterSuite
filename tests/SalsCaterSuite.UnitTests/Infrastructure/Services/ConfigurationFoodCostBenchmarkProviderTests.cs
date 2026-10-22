namespace SalsCaterSuite.UnitTests.Infrastructure.Services;

using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using SalsCaterSuite.Infrastructure.Services;
using Xunit;

/// <summary>
/// The deployment-configured margin benchmark (DEC-040, AC-B7). The benchmark is read in
/// exactly one place, so the menu-costing service and the operations dashboard can never
/// report a different target percentage.
/// </summary>
public class ConfigurationFoodCostBenchmarkProviderTests
{
    [Fact]
    public void A_configured_value_should_be_returned_and_flagged_as_configured()
    {
        var provider = Build(("Dashboard:Benchmarks:TargetFoodCostPercentage", "40"));

        provider.Current.TargetFoodCostPercentage.Should().Be(40m);
        provider.Current.TargetMarginPercentage.Should().Be(60m, "100 − 40");
        provider.Current.IsConfigured.Should().BeTrue();
    }

    [Fact]
    public void An_absent_key_should_fall_back_to_the_default_and_flag_that_it_is_not_configured()
    {
        var provider = Build();

        provider.Current.TargetFoodCostPercentage.Should().Be(35m, "the BR-07 conservative default");
        provider.Current.TargetMarginPercentage.Should().Be(65m);
        provider.Current.IsConfigured.Should().BeFalse();
    }

    [Fact]
    public void A_malformed_value_should_fall_back_to_the_default_instead_of_failing_the_request()
    {
        var provider = Build(("Dashboard:Benchmarks:TargetFoodCostPercentage", "not-a-number"));

        provider.Current.TargetFoodCostPercentage.Should().Be(35m);
        provider.Current.IsConfigured.Should().BeFalse();
    }

    [Fact]
    public void The_decimal_separator_of_the_current_culture_should_not_be_misread()
    {
        // "35.0" must be read as 35.0 regardless of the machine's regional settings.
        var provider = Build(("Dashboard:Benchmarks:TargetFoodCostPercentage", "35.0"));

        provider.Current.TargetFoodCostPercentage.Should().Be(35m);
    }

    private static ConfigurationFoodCostBenchmarkProvider Build(params (string Key, string? Value)[] settings)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(settings.ToDictionary(s => s.Key, s => s.Value))
            .Build();

        return new ConfigurationFoodCostBenchmarkProvider(
            configuration,
            NullLogger<ConfigurationFoodCostBenchmarkProvider>.Instance);
    }
}
