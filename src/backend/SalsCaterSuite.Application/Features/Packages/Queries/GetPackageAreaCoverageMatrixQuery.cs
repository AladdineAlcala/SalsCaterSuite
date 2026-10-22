using SalsCaterSuite.Application.Common.Interfaces;
using SalsCaterSuite.Application.Common.Models;
using SalsCaterSuite.Application.Features.Packages.DTOs;
using SalsCaterSuite.Application.Features.Packages.Services;

namespace SalsCaterSuite.Application.Features.Packages.Queries;

/// <summary>
/// GET /api/packages/{id}/areas — the tier's coverage matrix (contract §13.4, REQ-PKG-02).
/// <para>
/// The tier is resolved through the catalogue population (<c>IsDeleted == false</c>, DEC-074).
/// </para>
/// <para>
/// The gross-profit range states its own inputs (AC-P2-29): the food cost is the <b>standard
/// run</b>, the headcount is the tier's <b>floor</b>, and a coverage row that yields no computable
/// figure (no standard run, or no stated logistics cost) is <b>excluded</b> from the range rather
/// than counted as zero. <c>grossProfitCoverageCount</c> says how many rows contributed.
/// </para>
/// </summary>
public class GetPackageAreaCoverageMatrixQuery
{
    private readonly GetPackageCourseRulesetQuery _ruleset;
    private readonly IPackageAreaCoverageRepository _coverages;
    private readonly IDateTime _clock;

    public GetPackageAreaCoverageMatrixQuery(
        GetPackageCourseRulesetQuery ruleset,
        IPackageAreaCoverageRepository coverages,
        IDateTime clock)
    {
        _ruleset = ruleset;
        _coverages = coverages;
        _clock = clock;
    }

    public async Task<Result<PackageAreaCoverageMatrixDto>> ExecuteAsync(
        int packageId,
        CancellationToken cancellationToken = default)
    {
        // The ruleset carries the tier's identity, its rate, its floor and the standard run the
        // range is derived from — one source of truth for both workbenches (M2).
        var ruleset = await _ruleset.ExecuteAsync(packageId, cancellationToken);

        if (!ruleset.Succeeded)
        {
            return Result<PackageAreaCoverageMatrixDto>.Failure(ruleset.Errors);
        }

        var package = ruleset.Data!;

        var rows = await _coverages.GetLiveCoveragesAsync(packageId, cancellationToken);
        var activeRows = rows.Where(row => row.IsActive).ToList();

        // The surcharge range is measured over the ACTIVE coverage, and only over the rows that
        // carry a stated rate (contract §13.4).
        var rates = activeRows
            .Where(row => row.AdditionalRate.HasValue)
            .Select(row => row.AdditionalRate!.Value)
            .ToList();

        var grossProfitPercentages = ComputeGrossProfitRange(package, activeRows);

        var matrix = new PackageAreaCoverageMatrixDto
        {
            PackageId = package.PackageId,
            PackageName = package.PackageName,
            AmountPax = package.AmountPax,
            MinPax = package.MinPax,
            MinimumContractValue = package.MinimumContractValue,
            StandardRunFoodCostPerPax = package.StandardRunFoodCostPerPax,
            BaseGrossProfitPercentage = package.BaseGrossProfitPercentage,
            CoverageCount = rows.Count,
            ActiveCoverageCount = activeRows.Count,
            LowestAdditionalRate = rates.Count == 0 ? null : PackageCostingCalculator.Round(rates.Min()),
            HighestAdditionalRate = rates.Count == 0 ? null : PackageCostingCalculator.Round(rates.Max()),
            LowestGrossProfitPercentage = grossProfitPercentages.Count == 0
                ? null
                : PackageCostingCalculator.Round(grossProfitPercentages.Min()),
            HighestGrossProfitPercentage = grossProfitPercentages.Count == 0
                ? null
                : PackageCostingCalculator.Round(grossProfitPercentages.Max()),
            GrossProfitCoverageCount = grossProfitPercentages.Count,
            Areas = rows
                .OrderBy(row => row.AreaName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(row => row.AreaId)
                .Select(MapRow)
                .ToList(),
            GeneratedAt = _clock.UtcNow
        };

        return Result<PackageAreaCoverageMatrixDto>.Success(matrix);
    }

    /// <summary>
    /// The gross-profit percentage of every active coverage row that yields a computable figure.
    /// <para>
    /// The derivation: the food cost per pax is the <b>standard run</b>, the headcount is the
    /// tier's <b>floor</b> (<c>minPax</c>), the contract value is
    /// <c>(AmountPax + EffectiveAdditionalRate) × minPax</c> and the gross profit is
    /// <c>TotalContractValue − FoodCostTotal − LogisticsCost</c>. A row whose municipality has no
    /// stated logistics cost yields no figure and is excluded from the range (DEC-081).
    /// </para>
    /// </summary>
    private static List<decimal> ComputeGrossProfitRange(
        PackageCourseRulesetDto package,
        IReadOnlyCollection<PackageAreaCoverageProjection> activeRows)
    {
        var percentages = new List<decimal>();

        if (!package.MinPax.HasValue || !package.AmountPax.HasValue || !package.StandardRunFoodCostPerPax.HasValue)
        {
            // The range is null: the floor, the rate or the standard run is unknown.
            return percentages;
        }

        var minPax = package.MinPax.Value;

        foreach (var row in activeRows)
        {
            var totalContractValue = PackageCostingCalculator.TotalContractValue(
                package.AmountPax, row.AdditionalRate, minPax);
            var foodCostTotal = PackageCostingCalculator.FoodCostTotal(
                package.StandardRunFoodCostPerPax, minPax);
            var grossProfit = PackageCostingCalculator.GrossProfitWithLogistics(
                totalContractValue, foodCostTotal, row.LogisticsCost);
            var percentage = PackageCostingCalculator.GrossProfitPercentage(
                grossProfit, totalContractValue);

            if (percentage.HasValue)
            {
                percentages.Add(percentage.Value);
            }
        }

        return percentages;
    }

    private static PackageAreaCoverageRowDto MapRow(PackageAreaCoverageProjection row) => new()
    {
        PackageAreaCoverageId = row.PackageAreaCoverageId,
        AreaId = row.AreaId,
        AreaName = row.AreaName,
        // The municipality's rate, not a per-package override (DEC-078).
        AdditionalRate = row.AdditionalRate,
        DistanceKm = row.DistanceKm,
        LogisticsNote = row.LogisticsNote,
        LogisticsCost = row.LogisticsCost,
        IsActive = row.IsActive
    };
}
