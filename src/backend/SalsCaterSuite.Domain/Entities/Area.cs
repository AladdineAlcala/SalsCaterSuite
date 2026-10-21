using SalsCaterSuite.Domain.Common;

namespace SalsCaterSuite.Domain.Entities;

/// <summary>
/// A municipality / zone the kitchen can serve (REQ-PKG-02, DEC-077, DEC-081).
/// <para>
/// <see cref="AdditionalRate"/> is the <b>single</b> per-pax out-of-town surcharge — one rate per
/// municipality, not one rate per package (DEC-078). The coverage row that links a tier to a
/// municipality therefore carries no money column of its own.
/// </para>
/// </summary>
public class Area : BaseEntity
{
    public int AreaId { get; set; }
    public string AreaDetails { get; set; } = string.Empty;

    /// <summary>The per-pax out-of-town surcharge. <c>null</c> = no rate has been stated.</summary>
    public decimal? AdditionalRate { get; set; }

    /// <summary>≥ 0. The distance from the commissary, in kilometres. <c>null</c> = not stated.</summary>
    public int? DistanceKm { get; set; }

    /// <summary>≤ 150. The dispatch mode, e.g. "Reefer Truck &amp; Mobile Gen". <c>null</c> = not stated.</summary>
    public string? LogisticsNote { get; set; }

    /// <summary>
    /// The <b>per-event</b> logistics cost for this municipality (DEC-081). <c>null</c> = not
    /// stated, and the gross-profit split then <b>omits</b> the band rather than publishing zero.
    /// </summary>
    public decimal? LogisticsCost { get; set; }
}

