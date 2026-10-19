namespace SalsCaterSuite.Application.Common.Models;

/// <summary>
/// The per-course food-cost aggregate (KPI-16). One row per <c>CourseId</c> that has at least
/// one costed menu; a <c>null</c> <see cref="CourseId"/> is the "Uncategorised" group.
/// </summary>
/// <remarks>
/// Settable properties (rather than <c>init</c>) so the EF Core provider can materialise the
/// projection directly — the same convention the other repository projections use.
/// </remarks>
public record MenuCourseCostAggregate
{
    /// <summary>The course the menus belong to; <c>null</c> for uncategorised menus.</summary>
    public int? CourseId { get; set; }

    /// <summary>Σ <c>FoodCost</c> of the costed menus in this course.</summary>
    public decimal FoodCostAmount { get; set; }

    /// <summary>Number of costed menus in this course.</summary>
    public int MenuCount { get; set; }
}
