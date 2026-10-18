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
    public decimal TargetFoodCostPercentage { get; set; } = 35.0m;
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

