namespace SalsCaterSuite.Application.Common.Interfaces;

using SalsCaterSuite.Application.Common.Models;

/// <summary>
/// Supplies the deployment-configured food-cost benchmark (DEC-040).
/// <para>
/// The Application layer must not depend on <c>Microsoft.Extensions.Configuration</c>, so the
/// configuration binding lives in Infrastructure and is exposed through this neutral contract.
/// Both the operations dashboard and the menu-costing service consume it, which is what makes
/// AC-B8 ("the same target food-cost percentage") structurally true.
/// </para>
/// </summary>
public interface IFoodCostBenchmarkProvider
{
    /// <summary>The benchmark currently in force.</summary>
    FoodCostBenchmark Current { get; }
}

/// <summary>
/// The fallback benchmark used when no configuration provider is supplied — the BR-07 default
/// of 35 %, flagged as not configured.
/// </summary>
public sealed class DefaultFoodCostBenchmarkProvider : IFoodCostBenchmarkProvider
{
    /// <summary>A shared instance; the provider is stateless.</summary>
    public static DefaultFoodCostBenchmarkProvider Instance { get; } = new();

    public FoodCostBenchmark Current => FoodCostBenchmark.Default;
}
