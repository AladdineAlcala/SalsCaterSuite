using SalsCaterSuite.Application.Features.Packages.DTOs.Response;
namespace SalsCaterSuite.Application.Features.Packages.Models;

/// <summary>
/// The model of the printable specification sheet (contract §13.8, REQ-PKG-02).
/// <para>
/// It carries the two workbench payloads the sheet is rendered from, so the sheet and the
/// screens can never describe two different tiers.
/// </para>
/// </summary>
public class PackageSpecSheetModel
{
    /// <summary>The tier's course-coverage ruleset and its planning figures.</summary>
    public PackageCourseRulesetResponse Ruleset { get; set; } = new();

    /// <summary>The tier's coverage matrix.</summary>
    public PackageAreaCoverageMatrixResponse Coverage { get; set; } = new();
}
