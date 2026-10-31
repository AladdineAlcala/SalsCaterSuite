namespace SalsCaterSuite.Application.Features.Packages.DTOs.Response;

/// <summary>
/// The Packages &amp; Buffets catalogue aggregate (spec §3.2, DEC-068, KPI-25..KPI-28).
/// <para>
/// Every figure is computed <b>server-side</b>. The browser displays the aggregate; it never
/// derives a monetary figure of its own (DEC-002, workplan §3.5). The endpoint that publishes
/// it is <b>additive</b>: <c>GET /api/packages</c> keeps its shape and its <c>activeOnly</c>
/// filter, so no existing consumer breaks.
/// </para>
/// <para>
/// <see langword="null"/> means <b>unknown</b>, never zero (contract §9.2 rule 6). A catalogue
/// with no priced tier publishes <see langword="null"/> for the rate aggregate, and a catalogue
/// with no headcount floor publishes <see langword="null"/> for the floor range. The counts are
/// the exception: they are real measurements and are <c>0</c> for an empty catalogue.
/// </para>
/// </summary>
public class PackageCatalogueSummaryResponse
{
    /// <summary>KPI-25 — every non-soft-deleted tier in the catalogue.</summary>
    public int TotalPackages { get; set; }

    /// <summary>KPI-25 — the tiers that are selectable right now.</summary>
    public int ActivePackages { get; set; }

    /// <summary>KPI-25 — the tiers that are selectable no longer. <c>total − active</c>.</summary>
    public int InactivePackages { get; set; }

    /// <summary>KPI-26 — the tiers that carry a per-guest rate.</summary>
    public int PricedPackages { get; set; }

    /// <summary>
    /// KPI-26 — the mean of the priced population's rates, rounded to two decimals away from the
    /// midpoint (DEC-003). <see langword="null"/> when no tier is priced.
    /// </summary>
    public decimal? AverageAmountPax { get; set; }

    /// <summary>KPI-27 — the cheapest rate in the catalogue. <see langword="null"/> when no tier is priced.</summary>
    public decimal? LowestAmountPax { get; set; }

    /// <summary>KPI-27 — the dearest rate in the catalogue. <see langword="null"/> when no tier is priced.</summary>
    public decimal? HighestAmountPax { get; set; }

    /// <summary>KPI-28 — the smallest headcount floor. <see langword="null"/> when no tier sets one.</summary>
    public int? LowestMinPax { get; set; }

    /// <summary>KPI-28 — the largest headcount floor. <see langword="null"/> when no tier sets one.</summary>
    public int? HighestMinPax { get; set; }

    /// <summary>The server UTC timestamp at which the aggregate was produced.</summary>
    public DateTime GeneratedAt { get; set; }
}
