namespace SalsCaterSuite.Application.Features.Packages.DTOs.Response;

/// <summary>One dish of a rule's qualified pool (contract §13.1).</summary>
public class PackageCoursePoolDishResponse
{
    public string MenuId { get; set; } = string.Empty;

    public string MenuName { get; set; } = string.Empty;

    /// <summary>The dish's food cost. <c>null</c> or zero means the dish is not costed (DEC-087).</summary>
    public decimal? FoodCost { get; set; }

    public decimal? Price { get; set; }
}
