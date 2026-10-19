namespace SalsCaterSuite.Application.Common.Models;

/// <summary>
/// The menu-catalogue costing aggregate (KPI-14, KPI-15). Produced by the repository as a
/// server-side projection so the dashboard never has to materialise the whole catalogue.
/// <para>
/// <see cref="TotalCount"/> and <see cref="CostedCount"/> are measured over the non-deleted
/// menu catalogue; <see cref="CatalogueRevenueAmount"/> and
/// <see cref="CatalogueFoodCostAmount"/> are measured over the margin population only
/// (menus with a price and a food cost, both greater than zero).
/// </para>
/// </summary>
/// <remarks>
/// Settable properties (rather than <c>init</c>) so the EF Core provider can materialise the
/// projection directly — the same convention the other repository projections use.
/// </remarks>
public record MenuCostingAggregate
{
    /// <summary>Number of non-deleted menus in the catalogue.</summary>
    public int TotalCount { get; set; }

    /// <summary>Number of non-deleted menus whose food cost is known and greater than zero.</summary>
    public int CostedCount { get; set; }

    /// <summary>Σ <c>Price</c> over the margin population.</summary>
    public decimal CatalogueRevenueAmount { get; set; }

    /// <summary>Σ <c>FoodCost</c> over the margin population.</summary>
    public decimal CatalogueFoodCostAmount { get; set; }
}
