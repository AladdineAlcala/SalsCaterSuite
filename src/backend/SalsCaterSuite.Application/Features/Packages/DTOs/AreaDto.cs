namespace SalsCaterSuite.Application.Features.Packages.DTOs;

/// <summary>
/// A municipality of the coverage catalogue (contract §13.9, REQ-PKG-02).
/// <para>
/// <see cref="AdditionalRate"/> is the municipality's own per-pax out-of-town surcharge
/// (DEC-078) and <see cref="LogisticsCost"/> is its <b>per-event</b> logistics cost (DEC-081).
/// A <c>null</c> logistics cost is <b>not</b> a zero cost — the gross-profit split omits the band
/// instead of publishing zero.
/// </para>
/// </summary>
public class AreaDto
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

/// <summary>
/// The body of <c>POST /api/areas</c> and <c>PUT /api/areas/{id}</c> (contract §13.10, §13.11).
/// <para>
/// Every member except <see cref="AreaDetails"/> may be <c>null</c> — a null rate is "no rate has
/// been stated" and a null logistics cost omits the logistics band (DEC-086, DEC-081). The
/// defaults exist so a member that is <b>absent</b> from the payload reaches the validator
/// instead of the serializer (BR-11).
/// </para>
/// </summary>
public record AreaRequestDto(
    string AreaDetails = "",
    decimal? AdditionalRate = null,
    int? DistanceKm = null,
    string? LogisticsNote = null,
    decimal? LogisticsCost = null);
