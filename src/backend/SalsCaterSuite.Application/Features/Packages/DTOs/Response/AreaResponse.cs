namespace SalsCaterSuite.Application.Features.Packages.DTOs.Response;

/// <summary>
/// A municipality of the coverage catalogue (contract §13.9, REQ-PKG-02).
/// <para>
/// <see cref="AdditionalRate"/> is the municipality's own per-pax out-of-town surcharge
/// (DEC-078) and <see cref="LogisticsCost"/> is its <b>per-event</b> logistics cost (DEC-081).
/// A <c>null</c> logistics cost is <b>not</b> a zero cost — the gross-profit split omits the band
/// instead of publishing zero.
/// </para>
/// </summary>
public class AreaResponse
{
    public int AreaId { get; set; }

    /// <summary>The municipality / zone name.</summary>
    public string AreaDetails { get; set; } = string.Empty;

    /// <summary>The per-pax out-of-town surcharge. <c>null</c> = no rate has been stated.</summary>
    public decimal? AdditionalRate { get; set; }

    /// <summary>The distance from the commissary, in kilometres. <c>null</c> = not stated.</summary>
    public int? DistanceKm { get; set; }

    /// <summary>The dispatch mode. <c>null</c> = not stated.</summary>
    public string? LogisticsNote { get; set; }

    /// <summary>The per-event logistics cost. <c>null</c> = not stated.</summary>
    public decimal? LogisticsCost { get; set; }
}
