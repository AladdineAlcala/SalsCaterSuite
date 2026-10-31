namespace SalsCaterSuite.Application.Features.Menus.DTOs.Response;

public class MenuCostingSummaryResponse
{
    public string MenuId { get; set; } = string.Empty;
    public string MenuName { get; set; } = string.Empty;
    public decimal SellingPrice { get; set; }
    public decimal FoodCost { get; set; }
    public decimal GrossProfit { get; set; }
    public decimal FoodCostPercentage { get; set; }

    /// <summary>
    /// The gross margin percentage — <c>round((Price − FoodCost) ÷ Price × 100, 2)</c>, rounded
    /// away from zero, and <c>0</c> when the selling price is not above zero.
    /// <para>
    /// It is the same figure the Menu &amp; Food Costing Master's dish card publishes
    /// (<c>items[].grossMarginPercentage</c>): both come from the shared
    /// <c>MenuCostingCalculator</c>, so the two surfaces can never disagree. The client is
    /// forbidden from computing a margin (DEC-002), so the server publishes it.
    /// </para>
    /// </summary>
    public decimal GrossMarginPercentage { get; set; }

    /// <summary>
    /// The deployment-configured target food-cost percentage (DEC-040). The value is supplied
    /// by the costing service from <c>Dashboard:Benchmarks:TargetFoodCostPercentage</c>; the
    /// property itself carries no default so the benchmark exists in exactly one place.
    /// </summary>
    public decimal TargetFoodCostPercentage { get; set; }

    /// <summary>
    /// Whether the dish's food cost is within the configured target.
    /// <para>
    /// Unlike <c>MenuResponse.ProfitMargin</c> — which was removed because EF Core cannot project a
    /// get-only computed property and it therefore always serialised as <c>0</c> (RSK-030) —
    /// this DTO is <b>built in the service layer</b>, never projected by EF Core, so its computed
    /// properties do work over HTTP.
    /// </para>
    /// </summary>
    public bool IsWithinTarget => FoodCostPercentage <= TargetFoodCostPercentage;
}
