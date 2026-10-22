namespace SalsCaterSuite.UnitTests.Common.Fakes;

using SalsCaterSuite.Application.Common.Interfaces;
using SalsCaterSuite.Application.Common.Models;

/// <summary>
/// A hand-written <see cref="IFoodCostBenchmarkProvider"/> so the dashboard's benchmark can be
/// driven to either a configured value or the BR-07 default (DEC-040).
/// </summary>
public sealed class FakeFoodCostBenchmarkProvider : IFoodCostBenchmarkProvider
{
    public FakeFoodCostBenchmarkProvider()
        : this(FoodCostBenchmark.Default)
    {
    }

    public FakeFoodCostBenchmarkProvider(decimal targetFoodCostPercentage, bool isConfigured = true)
        : this(new FoodCostBenchmark(targetFoodCostPercentage, isConfigured))
    {
    }

    public FakeFoodCostBenchmarkProvider(FoodCostBenchmark benchmark)
    {
        Current = benchmark;
    }

    public FoodCostBenchmark Current { get; set; }
}
