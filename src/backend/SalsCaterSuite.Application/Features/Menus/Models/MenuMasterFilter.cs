namespace SalsCaterSuite.Application.Features.Menus.Models;

/// <summary>
/// The normalised filter of a Menu &amp; Food Costing Master read (spec §6.1).
/// <para>
/// The Application layer owns the <b>policy</b> (which raw query-string value means what, and
/// the clamping of the paging); the Infrastructure layer receives the already-normalised filter
/// and only has to translate it into SQL.
/// </para>
/// </summary>
public record MenuMasterFilter
{
    /// <summary>The case-insensitive substring over the dish code, the dish name and the note.</summary>
    public string? Search { get; init; }

    /// <summary>The course to restrict the result set to; <c>null</c> for the whole catalogue.</summary>
    public int? CourseId { get; init; }

    /// <summary>The status filter.</summary>
    public MenuMasterStatus Status { get; init; } = MenuMasterStatus.All;

    /// <summary>The sort key.</summary>
    public MenuMasterSort Sort { get; init; } = MenuMasterSort.FoodCostAsc;

    /// <summary>
    /// The configured target food-cost percentage (DEC-040). The margin-alert filter and the
    /// alert count are measured against it, so the filter and the KPI card can never disagree.
    /// </summary>
    public decimal TargetFoodCostPercentage { get; init; }

    /// <summary>The 1-based page to return.</summary>
    public int Page { get; init; } = 1;

    /// <summary>The number of rows a page holds.</summary>
    public int PageSize { get; init; } = 12;
}
