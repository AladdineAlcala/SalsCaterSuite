using System.Globalization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SalsCaterSuite.Application.Common.Interfaces;
using SalsCaterSuite.Application.Common.Models;

namespace SalsCaterSuite.Infrastructure.Services;

/// <summary>
/// Binds the deployment-configured food-cost benchmark (DEC-040).
/// <para>
/// The configuration key is <c>Dashboard:Benchmarks:TargetFoodCostPercentage</c>. When the key
/// is absent, the BR-07 conservative default of 35 % is substituted and
/// <see cref="FoodCostBenchmark.IsConfigured"/> is <c>false</c>, so the API can tell the client
/// that the figure is the default rather than a deployment decision.
/// </para>
/// <para>
/// This is the <b>only</b> place the benchmark is read, so the menu-costing service and the
/// operations dashboard can never report different target percentages (AC-B8).
/// </para>
/// </summary>
public class ConfigurationFoodCostBenchmarkProvider : IFoodCostBenchmarkProvider
{
    /// <summary>The configuration path of the target food-cost percentage.</summary>
    public const string ConfigurationPath = "Dashboard:Benchmarks:TargetFoodCostPercentage";

    private readonly IConfiguration _configuration;
    private readonly ILogger<ConfigurationFoodCostBenchmarkProvider> _logger;

    public ConfigurationFoodCostBenchmarkProvider(
        IConfiguration configuration,
        ILogger<ConfigurationFoodCostBenchmarkProvider> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public FoodCostBenchmark Current
    {
        get
        {
            var raw = _configuration[ConfigurationPath];

            if (string.IsNullOrWhiteSpace(raw))
            {
                return FoodCostBenchmark.Default;
            }

            if (!decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out var value))
            {
                // A malformed value must not take the dashboard down: fall back to the
                // conservative default and say so in the log.
                _logger.LogWarning(
                    "The configuration value at '{ConfigurationPath}' is not a valid decimal ('{RawValue}'); " +
                    "the default target food-cost percentage of {DefaultValue} is used instead.",
                    ConfigurationPath,
                    raw,
                    FoodCostBenchmark.DefaultTargetFoodCostPercentage);

                return FoodCostBenchmark.Default;
            }

            return new FoodCostBenchmark(value, IsConfigured: true);
        }
    }
}
