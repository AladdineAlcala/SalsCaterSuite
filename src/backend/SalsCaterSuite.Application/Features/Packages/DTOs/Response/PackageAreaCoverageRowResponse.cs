namespace SalsCaterSuite.Application.Features.Packages.DTOs.Response;

/// <summary>One covered municipality (contract §13.4).</summary>
public class PackageAreaCoverageRowResponse
{
    public int PackageAreaCoverageId { get; set; }

    public int AreaId { get; set; }

    /// <summary><c>Area.AreaDetails</c>.</summary>
    public string AreaName { get; set; } = string.Empty;

    /// <summary><c>Area.AdditionalRate</c> — the <b>municipality's</b> rate (DEC-078).</summary>
    public decimal? AdditionalRate { get; set; }

    /// <summary>The distance from the commissary. <c>null</c> = not stated.</summary>
    public int? DistanceKm { get; set; }

    /// <summary>The dispatch mode. <c>null</c> = not stated.</summary>
    public string? LogisticsNote { get; set; }

    /// <summary>The per-event logistics cost. <c>null</c> = not stated (the band is omitted, never zero).</summary>
    public decimal? LogisticsCost { get; set; }

    public bool IsActive { get; set; }
}
