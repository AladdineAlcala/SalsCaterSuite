namespace SalsCaterSuite.Application.Features.Menus.DTOs.Response;

/// <summary>
/// The server-computed margin preview (spec §6.2). The browser never computes a margin: the
/// form preview and the saved record are produced by the same arithmetic, so they can never
/// disagree (DEC-002).
/// </summary>
public class MenuCostingPreviewResponse
{
    /// <summary>The selling price that was previewed.</summary>
    public decimal SellingPrice { get; set; }

    /// <summary>The food cost that was previewed.</summary>
    public decimal FoodCost { get; set; }

    /// <summary><c>Price − FoodCost</c>.</summary>
    public decimal GrossProfit { get; set; }

    /// <summary><c>round(FoodCost ÷ Price × 100, 2)</c>; <c>0</c> when the price is zero.</summary>
    public decimal FoodCostPercentage { get; set; }

    /// <summary><c>round((Price − FoodCost) ÷ Price × 100, 2)</c>; <c>0</c> when the price is zero.</summary>
    public decimal GrossMarginPercentage { get; set; }

    /// <summary>The configured target food-cost percentage (DEC-040).</summary>
    public decimal TargetFoodCostPercentage { get; set; }

    /// <summary><c>100 − TargetFoodCostPercentage</c>.</summary>
    public decimal TargetMarginPercentage { get; set; }

    /// <summary><c>FoodCostPercentage ≤ TargetFoodCostPercentage</c>.</summary>
    public bool IsWithinTarget { get; set; }

    /// <summary><c>FoodCostPercentage − TargetFoodCostPercentage</c>, in percentage points.</summary>
    public decimal MarginVariancePercentage { get; set; }
}
