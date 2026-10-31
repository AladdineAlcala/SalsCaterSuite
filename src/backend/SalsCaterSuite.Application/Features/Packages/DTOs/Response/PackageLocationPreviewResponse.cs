namespace SalsCaterSuite.Application.Features.Packages.DTOs.Response;

/// <summary>
/// The out-of-town pricing simulator (contract §13.7, REQ-PKG-02).
/// <para>
/// A municipality with no stated rate still yields a complete contract value: the rate itself is
/// published as <c>null</c> ("not stated") and the money chain treats the absent surcharge as
/// zero (DEC-086). A municipality with no logistics cost <b>omits</b> the logistics band — the
/// gross profit is then <c>null</c> rather than overstated (DEC-081).
/// </para>
/// </summary>
public class PackageLocationPreviewResponse
{
    public int PackageId { get; set; }

    /// <summary>The requested headcount (≥ 1).</summary>
    public int Pax { get; set; }

    /// <summary>The per-guest rate. <c>null</c> = not priced.</summary>
    public decimal? AmountPax { get; set; }

    /// <summary>The selected municipality. <c>null</c> = no municipality was selected.</summary>
    public int? AreaId { get; set; }

    /// <summary>The municipality's name.</summary>
    public string? AreaName { get; set; }

    /// <summary>The municipality's per-pax surcharge. <c>null</c> when the municipality has stated no rate.</summary>
    public decimal? AdditionalRate { get; set; }

    /// <summary>
    /// The tier's own per-guest extended-location amount (the owner's 2026-10-24 request).
    /// <c>null</c> = not stated.
    /// </summary>
    public decimal? PackageExtendedLocationRate { get; set; }

    /// <summary><c>AmountPax × Pax</c>.</summary>
    public decimal? BaseTotal { get; set; }

    /// <summary><c>EffectiveAdditionalRate × Pax</c>. <c>null</c> when no municipality is selected.</summary>
    public decimal? ExtendedTotal { get; set; }

    /// <summary><c>AmountPax + EffectiveAdditionalRate</c>.</summary>
    public decimal? EffectiveRatePerPax { get; set; }

    /// <summary><c>BaseTotal + ExtendedTotal</c>.</summary>
    public decimal? TotalContractValue { get; set; }

    /// <summary>The selection's food cost, otherwise the standard run.</summary>
    public decimal? FoodCostPerPax { get; set; }

    /// <summary><c>FoodCostPerPax × Pax</c>.</summary>
    public decimal? FoodCostTotal { get; set; }

    /// <summary>The municipality's per-event logistics cost. <c>null</c> when it has none (the band is omitted).</summary>
    public decimal? LogisticsCost { get; set; }

    /// <summary><c>TotalContractValue − FoodCostTotal − LogisticsCost</c>.</summary>
    public decimal? GrossProfit { get; set; }

    /// <summary><c>GrossProfit ÷ TotalContractValue × 100</c>.</summary>
    public decimal? GrossProfitPercentage { get; set; }

    /// <summary>The server UTC timestamp of the payload.</summary>
    public DateTime GeneratedAt { get; set; }
}
