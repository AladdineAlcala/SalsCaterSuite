namespace SalsCaterSuite.Application.Features.Menus.DTOs;

/// <summary>
/// The Menu &amp; Food Costing Master payload (spec §6.1, REQ-MNU-01/REQ-MNU-02).
/// <para>
/// Every figure is computed <b>server-side</b> (DEC-002). The browser formats and displays; it
/// computes no margin, no percentage and no cost figure.
/// </para>
/// <para>
/// <see cref="Summary"/> describes the <b>whole filtered result set</b>, not the current page,
/// while <see cref="Courses"/> describes the <b>whole catalogue</b> (the course tab counts stay
/// stable while a scope is selected).
/// </para>
/// </summary>
public class MenuMasterDto
{
    /// <summary>The four KPI cards of the master screen — computed over the whole filtered set.</summary>
    public MenuMasterSummaryDto Summary { get; set; } = new();

    /// <summary>
    /// The course tab strip. Its scope is <b>everything except the course filter</b>: the
    /// dishes matching the search and the status filters, with no course filter. The tab
    /// counts therefore narrow with the search (the Customer Master's <c>quickViewCounts</c>
    /// pattern) and "All Dishes (n)" is exact (spec §6.1).
    /// </summary>
    public MenuCoursesDto Courses { get; set; } = new();

    /// <summary>The rows of the requested page.</summary>
    public List<MenuMasterItemDto> Items { get; set; } = new();

    /// <summary>The 1-based page that was returned.</summary>
    public int Page { get; set; }

    /// <summary>The page size that was applied.</summary>
    public int PageSize { get; set; }

    /// <summary>The number of dishes in the whole filtered set.</summary>
    public int TotalCount { get; set; }

    /// <summary><c>ceil(TotalCount ÷ PageSize)</c>.</summary>
    public int TotalPages { get; set; }

    /// <summary>The server UTC timestamp at which the aggregate was produced.</summary>
    public DateTime GeneratedAt { get; set; }
}

/// <summary>
/// The master's KPI cards (spec §4.2, §6.1).
/// <para>
/// The figures are measured over the <b>whole filtered result set</b>. The
/// <b>margin population</b> is the non-deleted dishes with <c>Price &gt; 0</c> <b>and</b>
/// <c>FoodCost &gt; 0</c> — the same population as KPI-15, so the master and the operations
/// dashboard can never disagree (DEC-040, AC-B8).
/// </para>
/// </summary>
public class MenuMasterSummaryDto
{
    /// <summary>KPI-14 — the number of dishes in the filtered set.</summary>
    public int TotalDishes { get; set; }

    /// <summary>KPI-14 — the number of filtered dishes whose food cost is known and above zero.</summary>
    public int CostedDishes { get; set; }

    /// <summary>The number of filtered dishes that carry no food cost.</summary>
    public int UncostedDishes { get; set; }

    /// <summary>
    /// KPI-14 — <c>costed ÷ total × 100</c>. <c>0</c> when the filtered set is empty: a
    /// population ratio with a defined zero (KPI-14's own zero-denominator rule).
    /// </summary>
    public decimal CostingCoveragePercentage { get; set; }

    /// <summary>
    /// KPI-22 — <c>Σ FoodCost ÷ Σ Price × 100</c> over the margin population.
    /// <c>null</c> when the margin population is empty — never <c>0</c> (contract §9.2 rule 6).
    /// </summary>
    public decimal? AverageFoodCostPercentage { get; set; }

    /// <summary>
    /// <c>target − actual</c>, in percentage points. <c>null</c> when the actual is
    /// <c>null</c>; <b>negative</b> when the catalogue is over target.
    /// </summary>
    public decimal? AverageFoodCostHeadroomPercentage { get; set; }

    /// <summary>
    /// The deployment-configured target food-cost percentage (DEC-040). The value comes from the
    /// single <c>IFoodCostBenchmarkProvider</c> the dashboard also reads.
    /// </summary>
    public decimal TargetFoodCostPercentage { get; set; }

    /// <summary><c>100 − TargetFoodCostPercentage</c>.</summary>
    public decimal TargetMarginPercentage { get; set; }

    /// <summary>
    /// <c>false</c> when the benchmark is the BR-07 default rather than a deployment decision.
    /// </summary>
    public bool IsBenchmarkConfigured { get; set; }

    /// <summary>
    /// <c>Σ Price ÷ |margin population|</c> over the margin population; <c>0</c> when that
    /// population is empty.
    /// </summary>
    public decimal AverageSellingPrice { get; set; }

    /// <summary>
    /// KPI-24 — the number of dishes in the filtered set whose food cost percentage exceeds the
    /// configured target.
    /// </summary>
    public int MarginAlertCount { get; set; }

    /// <summary>
    /// KPI-23 — the course with the highest <c>Σ(Price − FoodCost) ÷ Σ Price × 100</c> over its
    /// margin population. <c>null</c> when the margin population is empty. A dish without a
    /// course is grouped under <c>"Uncategorised"</c>, exactly as KPI-16 does.
    /// </summary>
    public MenuBestMarginCourseDto? BestMarginCourse { get; set; }
}

/// <summary>The best-margin course of the catalogue (KPI-23, spec §4.2 card 3).</summary>
public class MenuBestMarginCourseDto
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

/// <summary>
/// The course tab strip (spec §4.3, §6.1).
/// <para>
/// Its scope is <b>everything except the course filter</b> — the dishes matching the search
/// and the status filters. The tab counts therefore narrow with the search, and
/// <see cref="Total"/> is the exact "All Dishes (n)" count of the current scope.
/// </para>
/// </summary>
public class MenuCoursesDto
{
    /// <summary>
    /// The number of dishes in the tab strip's scope — the "All Dishes (n)" count. A dish
    /// without a course is included here even though it has no tab of its own, so
    /// <c>Σ items[].dishCount</c> may be smaller than this number.
    /// </summary>
    public int Total { get; set; }

    /// <summary>One entry per course category, with its live dish count.</summary>
    public List<MenuCourseCountDto> Items { get; set; } = new();
}

/// <summary>
/// One course tab (spec §4.3). The dish count is the count of the tab strip's scope — the
/// search and the status filters apply, the course filter does not — so the tab counts narrow
/// with the search and stay stable while a course is selected.
/// </summary>
public class MenuCourseCountDto
{
    /// <summary>The course id.</summary>
    public int CourseId { get; set; }

    /// <summary>The course name.</summary>
    public string CourseName { get; set; } = string.Empty;

    /// <summary>The number of dishes in the tab strip's scope that belong to this course.</summary>
    public int DishCount { get; set; }
}

/// <summary>One dish card of the master grid (spec §4.5, §6.1).</summary>
public class MenuMasterItemDto
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
    public decimal SellingPrice { get; set; }

    /// <summary>The food cost.</summary>
    public decimal FoodCost { get; set; }

    /// <summary><c>Price − FoodCost</c>.</summary>
    public decimal GrossProfit { get; set; }

    /// <summary><c>round(FoodCost ÷ Price × 100, 2)</c> — the same figure the costing service reports.</summary>
    public decimal FoodCostPercentage { get; set; }

    /// <summary><c>round((Price − FoodCost) ÷ Price × 100, 2)</c>.</summary>
    public decimal GrossMarginPercentage { get; set; }

    /// <summary>The configured target food-cost percentage (DEC-040).</summary>
    public decimal TargetFoodCostPercentage { get; set; }

    /// <summary><c>FoodCostPercentage ≤ TargetFoodCostPercentage</c>.</summary>
    public bool IsWithinTarget { get; set; }

    /// <summary><c>FoodCostPercentage − TargetFoodCostPercentage</c>, in percentage points.</summary>
    public decimal MarginVariancePercentage { get; set; }
}

/// <summary>
/// The server-computed margin preview (spec §6.2). The browser never computes a margin: the
/// form preview and the saved record are produced by the same arithmetic, so they can never
/// disagree (DEC-002).
/// </summary>
public class MenuCostingPreviewDto
{
    /// <summary>The selling price that was previewed.</summary>
    public decimal SellingPrice { get; set; }

    /// <summary>The food cost that was previewed.</summary>
    public decimal FoodCost { get; set; }

    /// <summary><c>Price − FoodCost</c>.</summary>
    public decimal GrossProfit { get; set; }

    /// <summary><c>round(FoodCost ÷ Price × 100, 2)</c>; <c>0</c> when the price is zero.</summary>
    public decimal FoodCostPercentage { get; set; }

    /// <summary><c>round((Price − FoodCost) ÷ Price × 100, 2)</c>; <c>0</c> when the price is zero.</summary>
    public decimal GrossMarginPercentage { get; set; }

    /// <summary>The configured target food-cost percentage (DEC-040).</summary>
    public decimal TargetFoodCostPercentage { get; set; }

    /// <summary><c>100 − TargetFoodCostPercentage</c>.</summary>
    public decimal TargetMarginPercentage { get; set; }

    /// <summary><c>FoodCostPercentage ≤ TargetFoodCostPercentage</c>.</summary>
    public bool IsWithinTarget { get; set; }

    /// <summary><c>FoodCostPercentage − TargetFoodCostPercentage</c>, in percentage points.</summary>
    public decimal MarginVariancePercentage { get; set; }
}

/// <summary>
/// The request body of <c>POST /api/menus/costing/preview</c> (spec §6.2).
/// </summary>
public class MenuCostingPreviewRequestDto
{
    /// <summary>The selling price to preview; must not be negative.</summary>
    public decimal SellingPrice { get; set; }

    /// <summary>The food cost to preview; must not be negative.</summary>
    public decimal FoodCost { get; set; }
}
