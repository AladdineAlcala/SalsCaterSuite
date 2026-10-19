namespace SalsCaterSuite.Application.Features.Menus.DTOs;

public class MenuDto
{
    public string MenuId { get; set; } = string.Empty;
    public int? CourseId { get; set; }
    public string? CourseName { get; set; }
    public string MenuName { get; set; } = string.Empty;
    public string? Note { get; set; }
    public decimal? Price { get; set; }
    public decimal? FoodCost { get; set; }
    public decimal ProfitMargin => (Price.GetValueOrDefault() > 0)
        ? Math.Round(((Price.GetValueOrDefault() - FoodCost.GetValueOrDefault()) / Price.GetValueOrDefault()) * 100m, 2)
        : 0m;
    public string? Image { get; set; }
}

public class MenuCostingSummaryDto
{
    public string MenuId { get; set; } = string.Empty;
    public string MenuName { get; set; } = string.Empty;
    public decimal SellingPrice { get; set; }
    public decimal FoodCost { get; set; }
    public decimal GrossProfit { get; set; }
    public decimal FoodCostPercentage { get; set; }

    /// <summary>
    /// The deployment-configured target food-cost percentage (DEC-040). The value is supplied
    /// by the costing service from <c>Dashboard:Benchmarks:TargetFoodCostPercentage</c>; the
    /// property itself carries no default so the benchmark exists in exactly one place.
    /// </summary>
    public decimal TargetFoodCostPercentage { get; set; }

    public bool IsWithinTarget => FoodCostPercentage <= TargetFoodCostPercentage;
}

public class CreateMenuRequestDto
{
    public string MenuId { get; set; } = string.Empty;
    public int CourseId { get; set; }
    public string MenuName { get; set; } = string.Empty;
    public string? Note { get; set; }
    public decimal Price { get; set; }
    public decimal FoodCost { get; set; }
}

/// <summary>Lightweight course-category projection used by GET /api/menus/courses.</summary>
public class CourseDto
{
    public int CourseId { get; set; }
    public string CourseName { get; set; } = string.Empty;
}

