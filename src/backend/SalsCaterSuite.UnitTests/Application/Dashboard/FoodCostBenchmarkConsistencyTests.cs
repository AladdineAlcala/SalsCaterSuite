namespace SalsCaterSuite.UnitTests.Application.Dashboard;

using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using SalsCaterSuite.Application.Common.Models;
using SalsCaterSuite.Application.Features.Menus.Services;
using SalsCaterSuite.Infrastructure.Services;
using SalsCaterSuite.UnitTests.Common.Fakes;
using Xunit;

/// <summary>
/// AC-B8 — the menu-costing service and the operations dashboard must report the same target
/// food-cost percentage. The test wires both consumers with the <b>same</b> configuration
/// provider, which is what makes the agreement structural rather than coincidental.
/// </summary>
public class FoodCostBenchmarkConsistencyTests
{
    [Theory]
    [InlineData("35.0")]
    [InlineData("40.0")]
    [InlineData("28.5")]
    public async Task The_menu_costing_service_and_the_dashboard_should_report_the_same_benchmark(string configured)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Dashboard:Benchmarks:TargetFoodCostPercentage"] = configured
            })
            .Build();

        var provider = new ConfigurationFoodCostBenchmarkProvider(
            configuration,
            NullLogger<ConfigurationFoodCostBenchmarkProvider>.Instance);

        var expected = decimal.Parse(configured, System.Globalization.CultureInfo.InvariantCulture);

        // The menu-costing service.
        var store = new FakeSalsCaterSuiteDataStore();
        store.Menus.Add(new Domain.Entities.Menu
        {
            MenuId = "M-001",
            MenuName = "Roast Beef",
            Price = 350m,
            FoodCost = 125m
        });

        var menuCosting = new MenuCostingService(new FakeMenuRepository(store), provider);
        var costing = await menuCosting.CalculateCostingAsync("M-001");

        costing.Data!.TargetFoodCostPercentage.Should().Be(expected);

        // The dashboard.
        var harness = new DashboardHarness(benchmarks: provider);
        var dashboard = await harness.Service.GetAsync(
            DashboardPeriodForHarness(),
            CancellationToken.None);

        dashboard.Data!.Benchmarks.TargetFoodCostPercentage.Should().Be(expected);
        dashboard.Data.Benchmarks.TargetMarginPercentage.Should().Be(100m - expected);
    }

    [Fact]
    public void The_default_benchmark_should_be_used_when_the_configuration_key_is_absent()
    {
        var configuration = new ConfigurationBuilder().Build();
        var provider = new ConfigurationFoodCostBenchmarkProvider(
            configuration,
            NullLogger<ConfigurationFoodCostBenchmarkProvider>.Instance);

        provider.Current.Should().Be(FoodCostBenchmark.Default);
        provider.Current.IsConfigured.Should().BeFalse();
    }

    private static SalsCaterSuite.Application.Features.Dashboard.Models.DashboardPeriod DashboardPeriodForHarness() =>
        SalsCaterSuite.Application.Features.Dashboard.Models.DashboardPeriod.ForMonth(2026, 10);
}
