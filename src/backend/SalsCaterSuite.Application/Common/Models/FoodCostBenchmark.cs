namespace SalsCaterSuite.Application.Common.Models;

/// <summary>
/// The deployment-configured margin benchmark (DEC-040). The margin benchmark is configuration,
/// not a hardcoded constant: <c>Dashboard:Benchmarks:TargetFoodCostPercentage</c> is bound at
/// start-up and read by both the operations dashboard and the menu-costing service, so the two
/// screens can never disagree (AC-B8).
/// <para>
/// The default of 35 % is the conservative end of the BR-07 band ("food cost should not exceed
/// 35–40 % of selling price") and preserves the behaviour that the menu-costing service had
/// before the benchmark became configurable.
/// </para>
/// </summary>
public record FoodCostBenchmark(decimal TargetFoodCostPercentage, bool IsConfigured)
{
    /// <summary>The BR-07 conservative default used when the configuration key is absent.</summary>
    public const decimal DefaultTargetFoodCostPercentage = 35.0m;

    /// <summary>The benchmark that is used when no configuration provider is available.</summary>
    public static FoodCostBenchmark Default { get; } = new(DefaultTargetFoodCostPercentage, false);

    /// <summary><c>100 − TargetFoodCostPercentage</c> (contract §9.1).</summary>
    public decimal TargetMarginPercentage => 100m - TargetFoodCostPercentage;
}
