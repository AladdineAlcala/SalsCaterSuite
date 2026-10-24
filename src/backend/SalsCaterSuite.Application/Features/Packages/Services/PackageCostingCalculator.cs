namespace SalsCaterSuite.Application.Features.Packages.Services;

/// <summary>
/// The <b>single</b> authoritative arithmetic of the Package Configuration Workbench
/// (REQ-PKG-02, DEC-002, DEC-003, BR-14 .. BR-20).
/// <para>
/// The ruleset read, the coverage matrix, the selection simulator, the location simulator and
/// the printable specification sheet all call these functions, so a figure can never differ
/// between the screen, the preview and the sheet. This is the same pattern the menu costing
/// follows with <c>MenuCostingCalculator</c>.
/// </para>
/// <para><b>Rounding (DEC-003, RSK-029).</b> Every published money figure and every published
/// percentage is rounded to two decimals, <b>away from the midpoint</b>, exactly <b>once</b>. The
/// browser never re-rounds. The only place where the rounding order is observable is the
/// <b>standard run</b>: the pool means are computed at full precision and the <b>sum</b> is
/// rounded once — summing the already-rounded means and rounding again would round twice and
/// could differ by a cent (DEC-083).
/// </para>
/// <para><b>Null semantics (contract §9.2 rule 6, BR-20).</b> A published figure is <c>null</c>
/// when any of its operands is unknown, and a percentage is <c>null</c> when its denominator is
/// unknown <b>or is zero</b>. <c>null</c> means <i>unknown</i>, never zero. A <c>0</c> is published
/// only where zero is a real measurement (an empty coverage set, a zero surcharge, a zero ceiling
/// overage).
/// </para>
/// <para><b>The one defined default (DEC-086).</b> An unstated out-of-town rate is published as
/// <c>null</c> — it is an unknown <i>measurement</i> — but the money chain treats the absent
/// surcharge as <b>zero</b>, because a surcharge is an <i>addition</i> and an absent addition is
/// nothing. A municipality with no stated rate therefore still yields a complete contract value.
/// </para>
/// </summary>
public static class PackageCostingCalculator
{
    /// <summary>
    /// The one rounding rule of the workbench: two decimals, away from the midpoint (DEC-003).
    /// </summary>
    public static decimal Round(decimal value) =>
        Math.Round(value, 2, MidpointRounding.AwayFromZero);

    // ─────────────────────────── the cost ceiling (BR-14) ───────────────────────────

    /// <summary>
    /// <c>AmountPax × TargetFoodCostPercentage ÷ 100</c> — the warning threshold of a tier.
    /// <c>null</c> when the tier carries no rate.
    /// </summary>
    public static decimal? CostCeilingPerPax(decimal? amountPax, decimal targetFoodCostPercentage) =>
        amountPax is null
            ? null
            : Round(amountPax.Value * targetFoodCostPercentage / 100m);

    /// <summary>
    /// <c>SelectedFoodCostPerPax ÷ AmountPax × 100</c>. <c>null</c> when the selected cost is
    /// unknown, or when the rate is unknown or zero.
    /// </summary>
    public static decimal? EffectiveFoodCostPercentage(decimal? selectedFoodCostPerPax, decimal? amountPax) =>
        selectedFoodCostPerPax is null || amountPax is null || amountPax.Value <= 0m
            ? null
            : Round(selectedFoodCostPerPax.Value / amountPax.Value * 100m);

    /// <summary>
    /// Whether the selection breaches the cost ceiling. A breach <b>warns</b>: it never reprices
    /// and it never blocks (DEC-080). It is <c>false</c> when the selected cost or the ceiling is
    /// unknown, because there is no known figure to compare.
    /// </summary>
    public static bool IsCeilingBreached(decimal? selectedFoodCostPerPax, decimal? costCeilingPerPax) =>
        selectedFoodCostPerPax.HasValue
        && costCeilingPerPax.HasValue
        && selectedFoodCostPerPax.Value > costCeilingPerPax.Value;

    /// <summary>
    /// <c>max(0, SelectedFoodCostPerPax − CostCeilingPerPax)</c> — the peso shortfall. <c>null</c>
    /// when the selected cost or the ceiling is unknown.
    /// </summary>
    public static decimal? CeilingShortfallPerPax(decimal? selectedFoodCostPerPax, decimal? costCeilingPerPax)
    {
        if (!selectedFoodCostPerPax.HasValue || !costCeilingPerPax.HasValue)
        {
            return null;
        }

        var over = selectedFoodCostPerPax.Value - costCeilingPerPax.Value;

        return Round(over > 0m ? over : 0m);
    }

    /// <summary>
    /// <c>max(0, SelectedFoodCostPerPax − CostCeilingPerPax) ÷ CostCeilingPerPax × 100</c> — the
    /// <b>percentage over the ceiling</b> (DEC-080). <c>null</c> when the selected cost is
    /// unknown, or when the ceiling is unknown or zero.
    /// </summary>
    public static decimal? CeilingOveragePercentage(decimal? selectedFoodCostPerPax, decimal? costCeilingPerPax)
    {
        if (!selectedFoodCostPerPax.HasValue || !costCeilingPerPax.HasValue || costCeilingPerPax.Value <= 0m)
        {
            return null;
        }

        var over = selectedFoodCostPerPax.Value - costCeilingPerPax.Value;
        if (over <= 0m)
        {
            // Zero overage is a real measurement, not an unknown.
            return Round(0m);
        }

        return Round(over / costCeilingPerPax.Value * 100m);
    }

    // ─────────────────────────── the derived floor (BR-16) ───────────────────────────

    /// <summary>
    /// <c>AmountPax × MinPax</c> — the minimum contract value of a tier (DEC-079). It is a floor
    /// on the contract, not a charge, and it never enters a booking total. <c>null</c> when
    /// either figure is unset.
    /// </summary>
    public static decimal? MinimumContractValue(decimal? amountPax, int? minPax) =>
        amountPax is null || minPax is null
            ? null
            : Round(amountPax.Value * minPax.Value);

    // ─────────────────────────── the pools and the standard run (BR-19) ───────────────────────────

    /// <summary>
    /// The mean food cost of a qualified pool's <b>costed</b> dishes. A dish is costed when its
    /// food cost is <b>greater than zero</b> (DEC-087) — the column stores zero rather than null
    /// when no cost was entered.
    /// <c>null</c> when no dish of the pool is costed.
    /// </summary>
    public static decimal? PoolAverageFoodCost(IEnumerable<decimal?> foodCosts)
    {
        var costed = CostedFoodCosts(foodCosts);

        return costed.Count == 0
            ? null
            : Round(costed.Sum() / costed.Count);
    }

    /// <summary>
    /// The <b>standard run</b>: the mean of each rule's qualified costed pool, summed (DEC-083).
    /// <para>
    /// The pool means are computed at <b>full precision</b> and the <b>sum</b> is rounded
    /// <b>once</b>. The published per-pool averages are separately rounded, but the standard run
    /// does <b>not</b> sum the rounded means — that would round twice and could differ from the
    /// single-rounding rule by a cent.
    /// </para>
    /// <para>
    /// <c>null</c> when the tier has no course rule, or when no rule's pool contains a costed
    /// dish.
    /// </para>
    /// </summary>
    public static decimal? StandardRunFoodCostPerPax(IEnumerable<IEnumerable<decimal?>> pools)
    {
        decimal sum = 0m;
        var anyCostedPool = false;

        foreach (var pool in pools)
        {
            var costed = CostedFoodCosts(pool);
            if (costed.Count == 0)
            {
                continue;
            }

            // The mean at full precision — the rounding happens once, on the sum.
            sum += costed.Sum() / costed.Count;
            anyCostedPool = true;
        }

        return anyCostedPool ? Round(sum) : null;
    }

    /// <summary>
    /// <c>StandardRunFoodCostPerPax ÷ AmountPax × 100</c>. <c>null</c> when the standard run is
    /// unknown, or when the rate is unknown or zero.
    /// </summary>
    public static decimal? StandardRunFoodCostPercentage(decimal? standardRunFoodCostPerPax, decimal? amountPax) =>
        standardRunFoodCostPerPax is null || amountPax is null || amountPax.Value <= 0m
            ? null
            : Round(standardRunFoodCostPerPax.Value / amountPax.Value * 100m);

    /// <summary>
    /// <c>(AmountPax − StandardRunFoodCostPerPax) ÷ AmountPax × 100</c>. <c>null</c> when the
    /// standard run is unknown, or when the rate is unknown or zero.
    /// </summary>
    public static decimal? BaseGrossProfitPercentage(decimal? amountPax, decimal? standardRunFoodCostPerPax) =>
        standardRunFoodCostPerPax is null || amountPax is null || amountPax.Value <= 0m
            ? null
            : Round((amountPax.Value - standardRunFoodCostPerPax.Value) / amountPax.Value * 100m);

    // ─────────────────────────── the selection (BR-15, DEC-087) ───────────────────────────

    /// <summary>
    /// The sum of the selected dishes' food costs. <c>null</c> when nothing is selected, or when
    /// <b>any</b> selected dish is not costed — the food cost of that selection is then
    /// <i>unknown</i>, never zero (DEC-087).
    /// </summary>
    public static decimal? SelectedFoodCostPerPax(IEnumerable<decimal?> foodCosts)
    {
        var costs = foodCosts.ToList();

        if (costs.Count == 0)
        {
            return null;
        }

        if (costs.Any(foodCost => !foodCost.HasValue || foodCost.Value <= 0m))
        {
            // One uncosted dish makes the whole selection's food cost unknown.
            return null;
        }

        return Round(costs.Where(foodCost => foodCost.HasValue).Sum(foodCost => foodCost!.Value));
    }

    /// <summary>
    /// The food cost per pax a preview reports: the <b>selection's</b> own cost when a selection
    /// was supplied, otherwise the <b>standard run</b>.
    /// <para>
    /// When a selection was supplied and one of its dishes is not costed, the selection's food
    /// cost is <i>unknown</i> and the figure stays <c>null</c> — the standard run is <b>not</b>
    /// substituted for it, because it is a <b>different</b> figure and showing it would be
    /// misleading (DEC-087).
    /// </para>
    /// </summary>
    public static decimal? ResolvedFoodCostPerPax(
        bool hasSelection,
        decimal? selectedFoodCostPerPax,
        decimal? standardRunFoodCostPerPax) =>
        hasSelection
            ? selectedFoodCostPerPax
            : standardRunFoodCostPerPax;

    // ─────────────────────────── the stress preview ───────────────────────────

    /// <summary>
    /// <c>SelectedFoodCostPerPax × (1 + StressPercentage ÷ 100)</c>. A <c>null</c> stress
    /// percentage means no stress (a factor of 1) and a zero stress percentage is the same.
    /// <c>null</c> when the selected cost is unknown.
    /// </summary>
    public static decimal? StressedFoodCostPerPax(decimal? selectedFoodCostPerPax, decimal? stressPercentage)
    {
        if (!selectedFoodCostPerPax.HasValue)
        {
            return null;
        }

        var factor = 1m + (stressPercentage ?? 0m) / 100m;

        return Round(selectedFoodCostPerPax.Value * factor);
    }

    /// <summary>
    /// <c>StressedFoodCostPerPax ÷ AmountPax × 100</c>. <c>null</c> when the stressed cost is
    /// unknown, or when the rate is unknown or zero.
    /// </summary>
    public static decimal? StressedFoodCostPercentage(decimal? stressedFoodCostPerPax, decimal? amountPax) =>
        stressedFoodCostPerPax is null || amountPax is null || amountPax.Value <= 0m
            ? null
            : Round(stressedFoodCostPerPax.Value / amountPax.Value * 100m);

    // ─────────────────────────── the headcount figures ───────────────────────────

    /// <summary>
    /// <c>AmountPax × Pax</c> — the base total of an event. <c>null</c> when the rate is unknown
    /// or no headcount is resolved.
    /// </summary>
    public static decimal? BaseTotal(decimal? amountPax, int? pax) =>
        amountPax is null || pax is null
            ? null
            : Round(amountPax.Value * pax.Value);

    /// <summary><c>AmountPax × Pax</c> — the gross revenue of the simulator.</summary>
    public static decimal? GrossRevenue(decimal? amountPax, int? pax) => BaseTotal(amountPax, pax);

    /// <summary>
    /// <c>FoodCostPerPax × Pax</c> — the food cost of the whole event. <c>null</c> when the food
    /// cost per pax is unknown or no headcount is resolved.
    /// </summary>
    public static decimal? FoodCostTotal(decimal? foodCostPerPax, int? pax) =>
        foodCostPerPax is null || pax is null
            ? null
            : Round(foodCostPerPax.Value * pax.Value);

    /// <summary>
    /// <c>GrossRevenue − FoodCostTotal</c>. <c>null</c> when either operand is unknown.
    /// </summary>
    public static decimal? GrossProfit(decimal? grossRevenue, decimal? foodCostTotal) =>
        grossRevenue is null || foodCostTotal is null
            ? null
            : Round(grossRevenue.Value - foodCostTotal.Value);

    /// <summary>
    /// <c>GrossProfit ÷ TotalContractValue × 100</c>. <c>null</c> when the gross profit is
    /// unknown, or when the contract value is unknown or zero.
    /// </summary>
    public static decimal? GrossProfitPercentage(decimal? grossProfit, decimal? totalContractValue) =>
        grossProfit is null || totalContractValue is null || totalContractValue.Value <= 0m
            ? null
            : Round(grossProfit.Value / totalContractValue.Value * 100m);

    // ─────────────────────────── the location breakdown (BR-17, BR-18) ───────────────────────────

    /// <summary>
    /// The surcharge the money chain applies: the stated rate, or <b>zero</b> when the
    /// municipality has stated none (DEC-086). The rate itself is still published as
    /// <c>null</c> — the measurement is unknown even though the addition is nothing.
    /// </summary>
    public static decimal EffectiveAdditionalRate(decimal? additionalRate) => additionalRate ?? 0m;

    /// <summary>
    /// The combined per-guest out-of-town rate: the municipality's rate plus the tier's own
    /// extended-location amount (the owner's 2026-10-24 request). Either term absent contributes
    /// zero; the published component rates keep their own null semantics.
    /// </summary>
    public static decimal EffectiveAdditionalRate(decimal? additionalRate, decimal? packageExtendedLocationAmount) =>
        (additionalRate ?? 0m) + (packageExtendedLocationAmount ?? 0m);

    /// <summary>
    /// <c>EffectiveAdditionalRate × Pax</c>. <c>null</c> when no headcount is resolved.
    /// </summary>
    public static decimal? ExtendedTotal(decimal? additionalRate, int? pax) =>
        pax is null
            ? null
            : Round(EffectiveAdditionalRate(additionalRate) * pax.Value);

    /// <summary>
    /// <c>(Area rate + tier amount) × Pax</c>. <c>null</c> when no headcount is resolved.
    /// </summary>
    public static decimal? ExtendedTotal(decimal? additionalRate, decimal? packageExtendedLocationAmount, int? pax) =>
        pax is null
            ? null
            : Round(EffectiveAdditionalRate(additionalRate, packageExtendedLocationAmount) * pax.Value);

    /// <summary>
    /// <c>AmountPax + EffectiveAdditionalRate</c>. <c>null</c> when the tier carries no rate.
    /// </summary>
    public static decimal? EffectiveRatePerPax(decimal? amountPax, decimal? additionalRate) =>
        amountPax is null
            ? null
            : Round(amountPax.Value + EffectiveAdditionalRate(additionalRate));

    /// <summary>
    /// <c>AmountPax + (Area rate + tier amount)</c>. <c>null</c> when the tier carries no rate.
    /// </summary>
    public static decimal? EffectiveRatePerPax(
        decimal? amountPax,
        decimal? additionalRate,
        decimal? packageExtendedLocationAmount) =>
        amountPax is null
            ? null
            : Round(amountPax.Value + EffectiveAdditionalRate(additionalRate, packageExtendedLocationAmount));

    /// <summary>
    /// <c>BaseTotal + ExtendedTotal</c> — the contract value of the simulated event. <c>null</c>
    /// when the tier carries no rate or no headcount is resolved.
    /// </summary>
    public static decimal? TotalContractValue(decimal? amountPax, decimal? additionalRate, int? pax)
    {
        var baseTotal = BaseTotal(amountPax, pax);
        if (!baseTotal.HasValue)
        {
            return null;
        }

        var extendedTotal = ExtendedTotal(additionalRate, pax);
        if (!extendedTotal.HasValue)
        {
            return null;
        }

        return Round(baseTotal.Value + extendedTotal.Value);
    }

    /// <summary>
    /// <c>BaseTotal + ((Area rate + tier amount) × Pax)</c> — the contract value of the simulated
    /// event. <c>null</c> when the tier carries no rate or no headcount is resolved.
    /// </summary>
    public static decimal? TotalContractValue(
        decimal? amountPax,
        decimal? additionalRate,
        decimal? packageExtendedLocationAmount,
        int? pax)
    {
        var baseTotal = BaseTotal(amountPax, pax);
        if (!baseTotal.HasValue)
        {
            return null;
        }

        var extendedTotal = ExtendedTotal(additionalRate, packageExtendedLocationAmount, pax);
        if (!extendedTotal.HasValue)
        {
            return null;
        }

        return Round(baseTotal.Value + extendedTotal.Value);
    }

    /// <summary>
    /// <c>TotalContractValue − FoodCostTotal − LogisticsCost</c> — the gross-profit split
    /// (BR-18). <c>null</c> when <b>any</b> operand is unknown: a municipality with no stated
    /// logistics cost <b>omits</b> the band and the gross profit is unknown rather than
    /// overstated (DEC-081).
    /// </summary>
    public static decimal? GrossProfitWithLogistics(
        decimal? totalContractValue,
        decimal? foodCostTotal,
        decimal? logisticsCost) =>
        totalContractValue is null || foodCostTotal is null || logisticsCost is null
            ? null
            : Round(totalContractValue.Value - foodCostTotal.Value - logisticsCost.Value);

    // ─────────────────────────── the pool helper ───────────────────────────

    /// <summary>
    /// The costed food costs of a pool — the dishes whose food cost is <b>greater than zero</b>
    /// (DEC-087).
    /// </summary>
    private static List<decimal> CostedFoodCosts(IEnumerable<decimal?> foodCosts) =>
        foodCosts
            .Where(foodCost => foodCost.HasValue && foodCost.Value > 0m)
            .Select(foodCost => foodCost!.Value)
            .ToList();
}
