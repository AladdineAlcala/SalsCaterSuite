namespace SalsCaterSuite.Application.Features.Menus.Models;

/// <summary>
/// The sort key of the Menu &amp; Food Costing Master (spec §4.4, §6.1).
/// <para>
/// An unrecognised value falls back to <see cref="FoodCostAsc"/>. Every sort carries a stable
/// tiebreak on the dish code, so the order never depends on the storage order.
/// </para>
/// </summary>
public enum MenuMasterSort
{
    /// <summary>Food cost percentage, lowest first — the default.</summary>
    FoodCostAsc = 0,

    /// <summary>Food cost percentage, highest first.</summary>
    FoodCostDesc = 1,

    /// <summary>Selling price, highest first.</summary>
    PriceDesc = 2,

    /// <summary>Selling price, lowest first.</summary>
    PriceAsc = 3,

    /// <summary>Gross margin percentage, highest first.</summary>
    MarginDesc = 4,

    /// <summary>Dish name, A to Z.</summary>
    NameAsc = 5
}
