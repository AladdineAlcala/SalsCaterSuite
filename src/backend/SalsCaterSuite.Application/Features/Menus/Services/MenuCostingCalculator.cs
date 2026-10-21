namespace SalsCaterSuite.Application.Features.Menus.Services;

/// <summary>
/// The <b>single</b> authoritative menu-costing arithmetic (DEC-002, spec §3, §7.5 / RSK-029).
/// <para>
/// The master grid, the margin preview and the menu-costing service all call these three
/// functions, so a dish's card, its preview and its costing response can never disagree.
/// </para>
/// <para>
/// The definitions are the ones the existing costing service already used:
/// <list type="bullet">
/// <item><description><c>GrossProfit = Price − FoodCost</c>.</description></item>
/// <item><description><c>FoodCostPercentage = round(FoodCost ÷ Price × 100, 2)</c>, with
/// <c>Price &gt; 0</c> as the guard; a zero price yields <c>0</c>.</description></item>
/// <item><description><c>GrossMarginPercentage = round((Price − FoodCost) ÷ Price × 100, 2)</c>,
/// with the same guard.</description></item>
/// </list>
/// <para><b>Rounding (RSK-029).</b> Every percentage is rounded with
/// <see cref="MidpointRounding.AwayFromZero"/> — the same convention the operations dashboard
/// uses. The rounding mode is a published rule, not an implementation detail: a half-to-even
/// rounding here and an away-from-zero rounding on the dashboard would let the same dish be
/// reported with two different percentages, which is exactly the class of divergence DEC-025
/// and DEC-042 exist to prevent.</para>
/// </para>
/// </summary>
public static class MenuCostingCalculator
{
    /// <summary><c>Price − FoodCost</c>.</summary>
    public static decimal GrossProfit(decimal sellingPrice, decimal foodCost) =>
        sellingPrice - foodCost;

    /// <summary>
    /// <c>round(FoodCost ÷ Price × 100, 2)</c>, away from the midpoint. A selling price of
    /// zero (or less) yields <c>0</c>, so a dish without a price never implies a margin.
    /// </summary>
    public static decimal FoodCostPercentage(decimal sellingPrice, decimal foodCost) =>
        sellingPrice > 0m
            ? Math.Round((foodCost / sellingPrice) * 100m, 2, MidpointRounding.AwayFromZero)
            : 0m;

    /// <summary>
    /// <c>round((Price − FoodCost) ÷ Price × 100, 2)</c>, away from the midpoint. A selling
    /// price of zero (or less) yields <c>0</c>.
    /// </summary>
    public static decimal GrossMarginPercentage(decimal sellingPrice, decimal foodCost) =>
        sellingPrice > 0m
            ? Math.Round(((sellingPrice - foodCost) / sellingPrice) * 100m, 2, MidpointRounding.AwayFromZero)
            : 0m;
}
