namespace SalsCaterSuite.Application.Features.Menus.DTOs.Response;

/// <summary>
/// The master's KPI cards (spec §4.2, §6.1).
/// <para>
/// The figures are measured over the <b>whole filtered result set</b>. The
/// <b>margin population</b> is the non-deleted dishes with <c>Price &gt; 0</c> <b>and</b>
/// <c>FoodCost &gt; 0</c> — the same population as KPI-15, so the master and the operations
/// dashboard can never disagree (DEC-040, AC-B8).
/// </para>
/// </summary>
public class MenuMasterSummaryResponse
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
    public MenuBestMarginCourseResponse? BestMarginCourse { get; set; }
}
