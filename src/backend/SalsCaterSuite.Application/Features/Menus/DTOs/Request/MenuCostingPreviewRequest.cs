namespace SalsCaterSuite.Application.Features.Menus.DTOs.Request;

/// <summary>
/// The request body of <c>POST /api/menus/costing/preview</c> (spec §6.2).
/// </summary>
public class MenuCostingPreviewRequest
{
    /// <summary>The selling price to preview; must not be negative.</summary>
    public decimal SellingPrice { get; set; }

    /// <summary>The food cost to preview; must not be negative.</summary>
    public decimal FoodCost { get; set; }
}
