namespace SalsCaterSuite.Application.Features.Menus.Models;

using SalsCaterSuite.Application.Features.Menus.DTOs.Request;
using SalsCaterSuite.Application.Features.Menus.DTOs.Response;
using SalsCaterSuite.Application.Features.Menus.Models;

/// <summary>
/// The raw rows of a Menu &amp; Food Costing Master read — the columns the database supplies and
/// the Application layer turns into the published figures.
/// <para>
/// The per-dish rounding is <c>Math.Round(…, 2)</c>, which is not reliably translatable to SQL
/// Server, so the derived and rounded fields are composed in the Application layer. This is the
/// deviation DEC-039 already accepted for the customer statement.
/// </para>
/// </summary>
public record MenuMasterRow
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
    public decimal? Price { get; set; }

    /// <summary>The food cost.</summary>
    public decimal? FoodCost { get; set; }
}

/// <summary>
/// The aggregate figures of a filtered menu result set (spec §6.1).
/// <para>
/// The counts and the sums are measured over the <b>whole filtered set</b>, not over the current
/// page. The revenue and the food cost are measured over the <b>margin population</b> only —
/// the non-deleted dishes with <c>Price &gt; 0</c> and <c>FoodCost &gt; 0</c>.
/// </para>
/// </summary>
public record MenuMasterAggregate
{
    /// <summary>The number of dishes in the filtered set.</summary>
    public int TotalCount { get; set; }

    /// <summary>The number of filtered dishes whose food cost is known and above zero.</summary>
    public int CostedCount { get; set; }

    /// <summary>The number of margin-population dishes in the filtered set.</summary>
    public int MarginPopulationCount { get; set; }

    /// <summary>Σ <c>Price</c> over the margin population.</summary>
    public decimal RevenueAmount { get; set; }

    /// <summary>Σ <c>FoodCost</c> over the margin population.</summary>
    public decimal FoodCostAmount { get; set; }
}

/// <summary>
/// The margin figures of one course inside the filtered set (KPI-23). The grouping is done in
/// SQL; the course names are resolved by the Application layer, which keeps the query a simple,
/// translatable <c>GROUP BY</c>.
/// </summary>
public record MenuCourseMarginAggregate
{
    /// <summary>The course; <c>null</c> for the uncategorised group.</summary>
    public int? CourseId { get; set; }

    /// <summary>Σ <c>Price</c> over the course's margin population.</summary>
    public decimal RevenueAmount { get; set; }

    /// <summary>Σ <c>FoodCost</c> over the course's margin population.</summary>
    public decimal FoodCostAmount { get; set; }

    /// <summary>The number of margin-population dishes in the course.</summary>
    public int DishCount { get; set; }
}

/// <summary>
/// Everything one Menu &amp; Food Costing Master read needs: the page rows, the aggregate figures
/// of the whole filtered set, the per-course margin figures, the catalogue-wide course counts and
/// the margin-alert count.
/// </summary>
public record MenuMasterDataSet
{
    /// <summary>The rows of the requested page.</summary>
    public List<MenuMasterRow> Rows { get; set; } = new();

    /// <summary>The aggregate figures of the whole filtered set.</summary>
    public MenuMasterAggregate Aggregate { get; set; } = new();

    /// <summary>The margin figures per course, over the filtered set's margin population.</summary>
    public List<MenuCourseMarginAggregate> CourseMargins { get; set; } = new();

    /// <summary>
    /// The course tab strip: the scope's total and one entry per course category with its live
    /// dish count. The scope is everything except the course filter (spec §6.1).
    /// </summary>
    public MenuCoursesResponse Courses { get; set; } = new();

    /// <summary>
    /// KPI-24 — the number of dishes in the filtered set whose food cost percentage exceeds the
    /// configured target.
    /// </summary>
    public int MarginAlertCount { get; set; }
}
