namespace SalsCaterSuite.Application.Features.Menus.DTOs.Response;

/// <summary>One dish card of the master grid (spec §4.5, §6.1).</summary>
public class MenuMasterItemResponse
{
    /// <summary>The dish code.</summary>
    public string MenuId { get; set; } = string.Empty;

    /// <summary>The dish name.</summary>
    public string MenuName { get; set; } = string.Empty;

    /// <summary>The course id; <c>null</c> when the dish is not assigned to a course.</summary>
    public int? CourseId { get; set; }

    /// <summary>The course name; <c>null</c> when the dish is not assigned to a course.</summary>
    public string? CourseName { get; set; }

    /// <summary>The dish note.</summary>
    public string? Note { get; set; }

    /// <summary>The plating photo URL.</summary>
    public string? Image { get; set; }

    /// <summary>The selling price.</summary>
    public decimal SellingPrice { get; set; }

    /// <summary>The food cost.</summary>
    public decimal FoodCost { get; set; }

    /// <summary><c>Price − FoodCost</c>.</summary>
    public decimal GrossProfit { get; set; }

    /// <summary><c>round(FoodCost ÷ Price × 100, 2)</c> — the same figure the costing service reports.</summary>
    public decimal FoodCostPercentage { get; set; }

    /// <summary><c>round((Price − FoodCost) ÷ Price × 100, 2)</c>.</summary>
    public decimal GrossMarginPercentage { get; set; }

    /// <summary>The configured target food-cost percentage (DEC-040).</summary>
    public decimal TargetFoodCostPercentage { get; set; }

    /// <summary><c>FoodCostPercentage ≤ TargetFoodCostPercentage</c>.</summary>
    public bool IsWithinTarget { get; set; }

    /// <summary><c>FoodCostPercentage − TargetFoodCostPercentage</c>, in percentage points.</summary>
    public decimal MarginVariancePercentage { get; set; }
}
