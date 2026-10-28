namespace SalsCaterSuite.Application.Features.Packages.DTOs;

/// <summary>
/// The rule rows of a tier, with the course name resolved (DEC-037 — the repository contract
/// never leaks an EF Core type).
/// <para>
/// The retired upgrade add-on and its derived description, unit price and unit are gone
/// (DEC-172): a rule no longer references the add-on master, so the projection no longer
/// resolves one.
/// </para>
/// </summary>
public class PackageCourseRuleProjection
{
    public int PackageCourseRuleId { get; set; }

    public int PackageId { get; set; }

    public int CourseId { get; set; }

    public string CourseName { get; set; } = string.Empty;

    public int RequiredQuota { get; set; }

    public int? PortionGrams { get; set; }

    public string RuleDescription { get; set; } = string.Empty;
}

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
