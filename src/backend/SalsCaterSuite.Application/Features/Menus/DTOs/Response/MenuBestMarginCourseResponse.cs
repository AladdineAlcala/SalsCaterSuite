namespace SalsCaterSuite.Application.Features.Menus.DTOs.Response;

/// <summary>The best-margin course of the catalogue (KPI-23, spec §4.2 card 3).</summary>
public class MenuBestMarginCourseResponse
{
    /// <summary>The course; <c>null</c> for the uncategorised group.</summary>
    public int? CourseId { get; set; }

    /// <summary>The course name; <c>"Uncategorised"</c> for the uncategorised group.</summary>
    public string CourseName { get; set; } = string.Empty;

    /// <summary><c>Σ(Price − FoodCost) ÷ Σ Price × 100</c> over the course's margin population.</summary>
    public decimal GrossMarginPercentage { get; set; }

    /// <summary><c>Σ Price ÷ |population|</c> over the course's margin population.</summary>
    public decimal AverageSellingPrice { get; set; }

    /// <summary>The number of margin-population dishes in the course.</summary>
    public int DishCount { get; set; }
}
