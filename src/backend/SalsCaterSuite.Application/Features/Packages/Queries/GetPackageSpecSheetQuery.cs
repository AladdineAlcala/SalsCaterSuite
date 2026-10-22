using SalsCaterSuite.Application.Common.Interfaces;
using SalsCaterSuite.Application.Common.Models;
using SalsCaterSuite.Application.Features.Packages.DTOs;

namespace SalsCaterSuite.Application.Features.Packages.Queries;

/// <summary>
/// GET /api/packages/{id}/spec-sheet — the printable specification sheet (contract §13.8,
/// REQ-PKG-02).
/// <para>
/// The sheet is rendered from the very same two payloads the workbench screens read, so the
/// printed document and the screen can never describe two different tiers. Nothing is persisted
/// and nothing is computed here — the arithmetic belongs to
/// <c>PackageCostingCalculator</c> (DEC-002).
/// </para>
/// </summary>
public class GetPackageSpecSheetQuery
{
    private readonly GetPackageCourseRulesetQuery _ruleset;
    private readonly GetPackageAreaCoverageMatrixQuery _coverage;

    public GetPackageSpecSheetQuery(
        GetPackageCourseRulesetQuery ruleset,
        GetPackageAreaCoverageMatrixQuery coverage)
    {
        _ruleset = ruleset;
        _coverage = coverage;
    }

    public async Task<Result<PackageSpecSheetModel>> ExecuteAsync(
        int packageId,
        CancellationToken cancellationToken = default)
    {
        var ruleset = await _ruleset.ExecuteAsync(packageId, cancellationToken);

        if (!ruleset.Succeeded)
        {
            return Result<PackageSpecSheetModel>.Failure(ruleset.Errors);
        }

        var coverage = await _coverage.ExecuteAsync(packageId, cancellationToken);

        if (!coverage.Succeeded)
        {
            return Result<PackageSpecSheetModel>.Failure(coverage.Errors);
        }

        return Result<PackageSpecSheetModel>.Success(new PackageSpecSheetModel
        {
            Ruleset = ruleset.Data!,
            Coverage = coverage.Data!
        });
    }
}
