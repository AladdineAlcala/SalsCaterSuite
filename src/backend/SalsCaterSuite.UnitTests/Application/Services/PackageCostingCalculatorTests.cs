namespace SalsCaterSuite.UnitTests.Application.Services;

using FluentAssertions;
using SalsCaterSuite.Application.Features.Packages.Services;
using Xunit;

/// <summary>
/// The single authoritative arithmetic of the Package Configuration Workbench (REQ-PKG-02,
/// DEC-002, DEC-003, DEC-083, BR-14 .. BR-20).
/// <para>
/// The rounding rules are published rules, not implementation details: a half-to-even rounding
/// here and an away-from-zero rounding elsewhere would let the same figure be reported two
/// different ways, which is exactly the class of divergence DEC-025 and DEC-042 exist to prevent.
/// </para>
/// </summary>
public class PackageCostingCalculatorTests
{
    // ─────────────────────────── the rounding rule ───────────────────────────

    [Fact]
    public void Every_money_figure_should_be_rounded_away_from_the_midpoint()
    {
        // 2.345 is exactly the midpoint. Half-to-even would yield 2.34; the published rule is
        // away from zero (DEC-003).
        PackageCostingCalculator.Round(2.345m).Should().Be(2.35m);
        PackageCostingCalculator.Round(-2.345m).Should().Be(-2.35m);
        PackageCostingCalculator.Round(2.344m).Should().Be(2.34m);
        PackageCostingCalculator.Round(2.346m).Should().Be(2.35m);
    }

    // ─────────────────────────── the cost ceiling (BR-14) ───────────────────────────

    [Fact]
    public void The_cost_ceiling_should_be_the_rate_times_the_benchmark()
    {
        PackageCostingCalculator.CostCeilingPerPax(1450m, 35m).Should().Be(507.50m);
        PackageCostingCalculator.CostCeilingPerPax(1000m, 33.33m).Should().Be(333.30m);
    }

    [Fact]
    public void The_cost_ceiling_should_be_null_when_the_tier_carries_no_rate()
    {
        PackageCostingCalculator.CostCeilingPerPax(null, 35m).Should().BeNull();
    }

    [Fact]
    public void The_effective_food_cost_percentage_should_be_the_selected_cost_over_the_rate()
    {
        PackageCostingCalculator.EffectiveFoodCostPercentage(362.50m, 1450m).Should().Be(25.00m);
        PackageCostingCalculator.EffectiveFoodCostPercentage(362.40m, 1450m).Should().Be(24.99m);
    }

    [Theory]
    [InlineData(null, 1450d)]
    [InlineData(362.5d, null)]
    [InlineData(362.5d, 0d)]
    public void The_effective_food_cost_percentage_should_be_null_when_an_operand_is_unknown(
        double? selectedFoodCost,
        double? amountPax)
    {
        PackageCostingCalculator
            .EffectiveFoodCostPercentage(
                selectedFoodCost.HasValue ? (decimal?)selectedFoodCost.Value : null,
                amountPax.HasValue ? (decimal?)amountPax.Value : null)
            .Should().BeNull();
    }

    [Fact]
    public void A_ceiling_breach_should_be_reported_but_never_reprice_or_block()
    {
        // The breach is a warning: the figures are unchanged and the verdict is a flag (DEC-080).
        PackageCostingCalculator.IsCeilingBreached(507.51m, 507.50m).Should().BeTrue();
        PackageCostingCalculator.IsCeilingBreached(507.50m, 507.50m).Should().BeFalse();
        PackageCostingCalculator.IsCeilingBreached(400m, 507.50m).Should().BeFalse();
    }

    [Fact]
    public void A_ceiling_breach_should_be_false_when_there_is_no_known_cost_to_compare()
    {
        PackageCostingCalculator.IsCeilingBreached(null, 507.50m).Should().BeFalse();
        PackageCostingCalculator.IsCeilingBreached(900m, null).Should().BeFalse();
    }

    [Fact]
    public void The_ceiling_shortfall_should_be_the_peso_gap_and_never_a_negative_figure()
    {
        PackageCostingCalculator.CeilingShortfallPerPax(600m, 507.50m).Should().Be(92.50m);
        PackageCostingCalculator.CeilingShortfallPerPax(400m, 507.50m).Should().Be(0.00m);
    }

    [Fact]
    public void The_ceiling_shortfall_should_be_null_when_an_operand_is_unknown()
    {
        PackageCostingCalculator.CeilingShortfallPerPax(null, 507.50m).Should().BeNull();
        PackageCostingCalculator.CeilingShortfallPerPax(600m, null).Should().BeNull();
    }

    [Fact]
    public void The_ceiling_overage_percentage_should_be_the_overage_over_the_ceiling()
    {
        PackageCostingCalculator.CeilingOveragePercentage(600m, 507.50m).Should().Be(18.23m);
        PackageCostingCalculator.CeilingOveragePercentage(400m, 507.50m).Should().Be(0.00m);
    }

    [Fact]
    public void The_ceiling_overage_percentage_should_be_null_when_an_operand_is_unknown()
    {
        PackageCostingCalculator.CeilingOveragePercentage(null, 507.50m).Should().BeNull();
        PackageCostingCalculator.CeilingOveragePercentage(600m, null).Should().BeNull();
        PackageCostingCalculator.CeilingOveragePercentage(600m, 0m).Should().BeNull();
    }

    // ─────────────────────────── the derived floor (BR-16) ───────────────────────────

    [Fact]
    public void The_minimum_contract_value_should_be_the_rate_times_the_floor()
    {
        PackageCostingCalculator.MinimumContractValue(1450m, 150).Should().Be(217500.00m);
    }

    [Fact]
    public void The_minimum_contract_value_should_be_null_when_either_figure_is_unset()
    {
        PackageCostingCalculator.MinimumContractValue(null, 150).Should().BeNull();
        PackageCostingCalculator.MinimumContractValue(1450m, null).Should().BeNull();
        PackageCostingCalculator.MinimumContractValue(null, null).Should().BeNull();
    }

    // ─────────────────────────── the pools and the standard run (BR-19) ───────────────────────────

    [Fact]
    public void The_pool_average_should_be_measured_over_the_costed_dishes_only()
    {
        // A dish with a zero food cost is not costed (DEC-087) and contributes to no average.
        PackageCostingCalculator.PoolAverageFoodCost(new decimal?[] { 100m, 50m, 0m }).Should().Be(75.00m);
        PackageCostingCalculator.PoolAverageFoodCost(new decimal?[] { 0m, 0m }).Should().BeNull();
        PackageCostingCalculator.PoolAverageFoodCost(new decimal?[] { null }).Should().BeNull();
        PackageCostingCalculator.PoolAverageFoodCost(Array.Empty<decimal?>()).Should().BeNull();
    }

    [Fact]
    public void The_standard_run_should_be_the_sum_of_the_pool_means()
    {
        var pools = new[]
        {
            new decimal?[] { 100m, 200m },   // mean 150
            new decimal?[] { 50m, 100m }     // mean  75
        };

        PackageCostingCalculator.StandardRunFoodCostPerPax(pools).Should().Be(225.00m);
    }

    [Fact]
    public void The_standard_run_should_be_null_when_no_pool_holds_a_costed_dish()
    {
        PackageCostingCalculator.StandardRunFoodCostPerPax(Array.Empty<IEnumerable<decimal?>>()).Should().BeNull();
        PackageCostingCalculator.StandardRunFoodCostPerPax(new[] { new decimal?[] { 0m, 0m } }).Should().BeNull();
    }

    /// <summary>
    /// The <b>single-rounding order</b> of the standard run (DEC-083, RSK-029): the pool means are
    /// computed at full precision and the sum is rounded <b>once</b>. Summing the already-rounded
    /// means and rounding again would round twice and would differ here by a cent.
    /// </summary>
    [Fact]
    public void The_standard_run_should_round_the_sum_once_and_not_sum_the_rounded_means()
    {
        // Both means are exactly 10.005 and 20.005 — a midpoint each.
        var pools = new[]
        {
            new decimal?[] { 10.005m, 10.005m },
            new decimal?[] { 20.005m, 20.005m }
        };

        // 10.005 + 20.005 = 30.010 → 30.01 (one rounding).
        PackageCostingCalculator.StandardRunFoodCostPerPax(pools).Should().Be(30.01m);

        // The published per-pool averages are separately rounded: 10.01 and 20.01.
        var roundedMeans = pools
            .Select(pool => PackageCostingCalculator.PoolAverageFoodCost(pool))
            .ToList();

        roundedMeans.Should().BeEquivalentTo(new[] { 10.01m, 20.01m });

        // Summing the ROUNDED means would have produced 30.02 — the double-rounding answer the
        // rule forbids.
        (roundedMeans.Sum() ?? 0m).Should().Be(30.02m);
    }

    [Fact]
    public void The_standard_run_percentage_should_be_the_standard_run_over_the_rate()
    {
        PackageCostingCalculator.StandardRunFoodCostPercentage(225m, 1450m).Should().Be(15.52m);
    }

    [Fact]
    public void The_standard_run_percentage_should_be_null_when_an_operand_is_unknown()
    {
        PackageCostingCalculator.StandardRunFoodCostPercentage(null, 1450m).Should().BeNull();
        PackageCostingCalculator.StandardRunFoodCostPercentage(225m, null).Should().BeNull();
        PackageCostingCalculator.StandardRunFoodCostPercentage(225m, 0m).Should().BeNull();
    }

    [Fact]
    public void The_base_gross_profit_percentage_should_be_the_rate_less_the_standard_run()
    {
        PackageCostingCalculator.BaseGrossProfitPercentage(1450m, 225m).Should().Be(84.48m);
    }

    [Fact]
    public void The_base_gross_profit_percentage_should_be_null_when_an_operand_is_unknown()
    {
        PackageCostingCalculator.BaseGrossProfitPercentage(1450m, null).Should().BeNull();
        PackageCostingCalculator.BaseGrossProfitPercentage(null, 225m).Should().BeNull();
        PackageCostingCalculator.BaseGrossProfitPercentage(0m, 225m).Should().BeNull();
    }

    // ─────────────────────────── the selection (BR-15, DEC-087) ───────────────────────────

    [Fact]
    public void The_selected_food_cost_should_be_the_sum_of_the_selected_dishes()
    {
        PackageCostingCalculator.SelectedFoodCostPerPax(new decimal?[] { 100m, 50.25m }).Should().Be(150.25m);
    }

    [Fact]
    public void An_uncosted_selected_dish_should_make_the_selected_food_cost_unknown()
    {
        // Unknown — never zero (DEC-087).
        PackageCostingCalculator.SelectedFoodCostPerPax(new decimal?[] { 100m, 0m }).Should().BeNull();
        PackageCostingCalculator.SelectedFoodCostPerPax(new decimal?[] { 100m, null }).Should().BeNull();
    }

    [Fact]
    public void The_selected_food_cost_should_be_null_when_nothing_is_selected()
    {
        PackageCostingCalculator.SelectedFoodCostPerPax(Array.Empty<decimal?>()).Should().BeNull();
    }

    [Fact]
    public void The_resolved_food_cost_should_be_the_selection_or_the_standard_run()
    {
        // No selection supplied → the standard run.
        PackageCostingCalculator.ResolvedFoodCostPerPax(false, null, 225m).Should().Be(225m);

        // A selection supplied → the selection's own cost.
        PackageCostingCalculator.ResolvedFoodCostPerPax(true, 300m, 225m).Should().Be(300m);

        // A selection supplied but uncosted → unknown; the standard run is NOT substituted for it.
        PackageCostingCalculator.ResolvedFoodCostPerPax(true, null, 225m).Should().BeNull();
    }

    // ─────────────────────────── the stress preview ───────────────────────────

    [Fact]
    public void The_stress_preview_should_multiply_the_selected_cost_by_the_factor()
    {
        PackageCostingCalculator.StressedFoodCostPerPax(200m, 10m).Should().Be(220.00m);
    }

    [Fact]
    public void A_null_or_zero_stress_percentage_should_mean_no_stress()
    {
        PackageCostingCalculator.StressedFoodCostPerPax(200m, null).Should().Be(200.00m);
        PackageCostingCalculator.StressedFoodCostPerPax(200m, 0m).Should().Be(200.00m);
    }

    [Fact]
    public void The_stressed_cost_should_be_null_when_the_selected_cost_is_unknown()
    {
        PackageCostingCalculator.StressedFoodCostPerPax(null, 10m).Should().BeNull();
    }

    [Fact]
    public void The_stressed_percentage_should_be_the_stressed_cost_over_the_rate()
    {
        PackageCostingCalculator.StressedFoodCostPercentage(220m, 1000m).Should().Be(22.00m);
        PackageCostingCalculator.StressedFoodCostPercentage(null, 1000m).Should().BeNull();
        PackageCostingCalculator.StressedFoodCostPercentage(220m, 0m).Should().BeNull();
    }

    // ─────────────────────────── the headcount figures ───────────────────────────

    [Fact]
    public void The_headcount_figures_should_be_the_rate_and_the_food_cost_times_the_headcount()
    {
        PackageCostingCalculator.GrossRevenue(1450m, 150).Should().Be(217500.00m);
        PackageCostingCalculator.FoodCostTotal(225m, 150).Should().Be(33750.00m);
        PackageCostingCalculator.GrossProfit(217500m, 33750m).Should().Be(183750.00m);
        PackageCostingCalculator.GrossProfitPercentage(183750m, 217500m).Should().Be(84.48m);
    }

    [Fact]
    public void The_headcount_figures_should_be_null_when_an_operand_is_unknown()
    {
        PackageCostingCalculator.GrossRevenue(null, 150).Should().BeNull();
        PackageCostingCalculator.GrossRevenue(1450m, null).Should().BeNull();
        PackageCostingCalculator.FoodCostTotal(null, 150).Should().BeNull();
        PackageCostingCalculator.FoodCostTotal(225m, null).Should().BeNull();
        PackageCostingCalculator.GrossProfit(null, 33750m).Should().BeNull();
        PackageCostingCalculator.GrossProfit(217500m, null).Should().BeNull();
        PackageCostingCalculator.GrossProfitPercentage(183750m, null).Should().BeNull();
        PackageCostingCalculator.GrossProfitPercentage(183750m, 0m).Should().BeNull();
    }

    // ─────────────────────────── the location breakdown (BR-17, BR-18) ───────────────────────────

    [Fact]
    public void An_unstated_out_of_town_rate_should_be_published_as_null_but_cost_nothing()
    {
        // The rate is an unknown measurement (DEC-086)…
        PackageCostingCalculator.EffectiveAdditionalRate(null).Should().Be(0m);

        // …but the money chain treats the absent addition as zero, so the contract value is
        // still complete.
        PackageCostingCalculator.ExtendedTotal(null, 150).Should().Be(0.00m);
        PackageCostingCalculator.EffectiveRatePerPax(1450m, null).Should().Be(1450.00m);
        PackageCostingCalculator.TotalContractValue(1450m, null, 150).Should().Be(217500.00m);
    }

    [Fact]
    public void The_location_breakdown_should_split_the_base_and_the_extended_total()
    {
        PackageCostingCalculator.BaseTotal(1450m, 150).Should().Be(217500.00m);
        PackageCostingCalculator.ExtendedTotal(150m, 150).Should().Be(22500.00m);
        PackageCostingCalculator.EffectiveRatePerPax(1450m, 150m).Should().Be(1600.00m);
        PackageCostingCalculator.TotalContractValue(1450m, 150m, 150).Should().Be(240000.00m);
    }

    [Fact]
    public void The_location_breakdown_should_be_null_when_the_rate_or_the_headcount_is_unknown()
    {
        PackageCostingCalculator.BaseTotal(null, 150).Should().BeNull();
        PackageCostingCalculator.BaseTotal(1450m, null).Should().BeNull();
        PackageCostingCalculator.ExtendedTotal(150m, null).Should().BeNull();
        PackageCostingCalculator.EffectiveRatePerPax(null, 150m).Should().BeNull();
        PackageCostingCalculator.TotalContractValue(null, 150m, 150).Should().BeNull();
        PackageCostingCalculator.TotalContractValue(1450m, 150m, null).Should().BeNull();
    }

    [Fact]
    public void The_gross_profit_split_should_subtract_the_food_cost_and_the_logistics_cost()
    {
        PackageCostingCalculator
            .GrossProfitWithLogistics(240000m, 33750m, 3200m)
            .Should().Be(203050.00m);

        PackageCostingCalculator
            .GrossProfitPercentage(203050m, 240000m)
            .Should().Be(84.60m);
    }

    [Fact]
    public void The_gross_profit_split_should_be_null_when_the_logistics_cost_is_not_stated()
    {
        // The band is omitted, so the gross profit is unknown rather than overstated (DEC-081).
        PackageCostingCalculator.GrossProfitWithLogistics(240000m, 33750m, null).Should().BeNull();
        PackageCostingCalculator.GrossProfitWithLogistics(240000m, null, 3200m).Should().BeNull();
        PackageCostingCalculator.GrossProfitWithLogistics(null, 33750m, 3200m).Should().BeNull();
    }
}
