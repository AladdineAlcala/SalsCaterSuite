namespace SalsCaterSuite.Application.Features.Packages.Models;

/// <summary>
/// The coverage rows of a tier, with the municipality's details resolved.
/// <para>
/// A coverage row whose <b>municipality</b> is soft-deleted is excluded from the projection and
/// from every count — the municipality catalogue is the population (contract §13.4).
/// </para>
/// </summary>
public class PackageAreaCoverageProjection
{
    public int PackageAreaCoverageId { get; set; }

    public int PackageId { get; set; }

    public int AreaId { get; set; }

    public string AreaName { get; set; } = string.Empty;

    public decimal? AdditionalRate { get; set; }

    public int? DistanceKm { get; set; }

    public string? LogisticsNote { get; set; }

    public decimal? LogisticsCost { get; set; }

    public bool IsActive { get; set; }
}
