namespace SalsCaterSuite.Application.Features.Packages.DTOs;

/// <summary>
/// The tier's coverage matrix — the municipalities it may be served in (contract §13.4).
/// <para>
/// The rate a row quotes is the <b>municipality's</b> rate (DEC-078); the row itself carries no
/// money. The gross-profit range states its own inputs: the food cost is the <b>standard run</b>,
/// the headcount is the tier's <b>floor</b>, and a row that yields no computable figure is
/// excluded from the range rather than counted as zero (AC-P2-29).
/// </para>
/// </summary>
public class PackageAreaCoverageMatrixDto
{
    public int PackageId { get; set; }

    /// <summary><c>Package.Description</c>.</summary>
    public string PackageName { get; set; } = string.Empty;

    /// <summary>The per-guest rate. <c>null</c> = not priced.</summary>
    public decimal? AmountPax { get; set; }

    /// <summary>The headcount floor. <c>null</c> = not set.</summary>
    public int? MinPax { get; set; }

    /// <summary><c>AmountPax × MinPax</c> — the derived floor on the contract (DEC-079).</summary>
    public decimal? MinimumContractValue { get; set; }

    /// <summary>The tier's standard run, read from the course ruleset.</summary>
    public decimal? StandardRunFoodCostPerPax { get; set; }

    /// <summary><c>(AmountPax − StandardRunFoodCostPerPax) ÷ AmountPax × 100</c>.</summary>
    public decimal? BaseGrossProfitPercentage { get; set; }

    /// <summary>The tier's covered municipalities.</summary>
    public int CoverageCount { get; set; }

    /// <summary>The covered municipalities whose coverage is active.</summary>
    public int ActiveCoverageCount { get; set; }

    /// <summary>The smallest surcharge among the active coverage. <c>null</c> when none carries a rate.</summary>
    public decimal? LowestAdditionalRate { get; set; }

    /// <summary>The largest surcharge among the active coverage.</summary>
    public decimal? HighestAdditionalRate { get; set; }

    /// <summary>The smallest gross-profit percentage across the active coverage, at the tier's floor headcount.</summary>
    public decimal? LowestGrossProfitPercentage { get; set; }

    /// <summary>The largest gross-profit percentage across the same population and derivation.</summary>
    public decimal? HighestGrossProfitPercentage { get; set; }

    /// <summary>How many active coverage rows yielded a computable gross-profit figure.</summary>
    public int GrossProfitCoverageCount { get; set; }

    /// <summary>The coverage rows.</summary>
    public List<PackageAreaCoverageRowDto> Areas { get; set; } = new();

    /// <summary>The server UTC timestamp of the payload.</summary>
    public DateTime GeneratedAt { get; set; }
}

/// <summary>One covered municipality (contract §13.4).</summary>
public class PackageAreaCoverageRowDto
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

/// <summary>
/// The body of <c>PUT /api/packages/{id}/areas</c> (contract §13.5).
/// <para>
/// The <c>coverages</c> member is <b>nullable</b> on purpose: an absent or explicitly null member
/// is a rejected payload (<c>400</c>), never an empty set (DEC-085).
/// </para>
/// </summary>
public record PackageAreaCoverageRequest(
    IReadOnlyList<PackageAreaCoverageEntryDto>? Coverages = null);

/// <summary>One submitted coverage (contract §13.5).</summary>
public record PackageAreaCoverageEntryDto(
    int AreaId = 0,
    bool IsActive = true);

/// <summary>The body of <c>POST /api/packages/{id}/areas</c> (contract §13.6).</summary>
public record PackageAreaCoverageAttachDto(
    int AreaId = 0,
    bool IsActive = true);
