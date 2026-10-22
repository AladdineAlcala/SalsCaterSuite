using FluentValidation;
using SalsCaterSuite.Application.Common.Interfaces;
using SalsCaterSuite.Application.Common.Models;
using SalsCaterSuite.Application.Features.Packages.DTOs;
using SalsCaterSuite.Application.Features.Packages.Services;
using SalsCaterSuite.Domain.Entities;

namespace SalsCaterSuite.Application.Features.Packages.Queries;

/// <summary>
/// POST /api/packages/{id}/areas/preview — the out-of-town pricing simulator (contract §13.7,
/// REQ-PKG-02).
/// <para>
/// A preview is a <b>what-if</b>: it persists nothing and it is not a booking total (BR-20). Every
/// figure is computed on the server through <see cref="PackageCostingCalculator"/> (DEC-002).
/// </para>
/// <para>
/// Two null semantics are load-bearing here. A municipality with <b>no stated rate</b> publishes
/// the rate as <c>null</c> — "not stated" — but the money chain treats the absent surcharge as
/// <b>zero</b>, so the contract value is still complete (DEC-086). A municipality with <b>no
/// stated logistics cost</b> <b>omits</b> the logistics band: the gross profit is then
/// <c>null</c> rather than overstated (DEC-081).
/// </para>
/// </summary>
public class PreviewPackageLocationQuery
{
    private readonly GetPackageCourseRulesetQuery _ruleset;
    private readonly IAreaRepository _areas;
    private readonly IMenuRepository _menus;
    private readonly IValidator<PackageLocationPreviewRequest> _validator;
    private readonly IDateTime _clock;

    public PreviewPackageLocationQuery(
        GetPackageCourseRulesetQuery ruleset,
        IAreaRepository areas,
        IMenuRepository menus,
        IValidator<PackageLocationPreviewRequest> validator,
        IDateTime clock)
    {
        _ruleset = ruleset;
        _areas = areas;
        _menus = menus;
        _validator = validator;
        _clock = clock;
    }

    public async Task<Result<PackageLocationPreviewDto>> ExecuteAsync(
        int packageId,
        PackageLocationPreviewRequest request,
        CancellationToken cancellationToken = default)
    {
        var validationResult = await _validator.ValidateAsync(request, cancellationToken);

        if (!validationResult.IsValid)
        {
            return Result<PackageLocationPreviewDto>.Failure(
                validationResult.Errors.Select(error => error.ErrorMessage).ToArray());
        }

        var ruleset = await _ruleset.ExecuteAsync(packageId, cancellationToken);

        if (!ruleset.Succeeded)
        {
            return new PackageNotFoundResult<PackageLocationPreviewDto>(
                ruleset.Errors.FirstOrDefault() ?? "Package not found.");
        }

        var package = ruleset.Data!;

        Area? area = null;
        if (request.AreaId.HasValue)
        {
            area = await _areas.GetLiveByIdAsync(request.AreaId.Value, cancellationToken);

            if (area is null)
            {
                // A missing municipality is a 404 (contract §13, common rules).
                return new AreaNotFoundResult<PackageLocationPreviewDto>("Area not found.");
            }
        }

        var menus = await _menus.GetOrderedByNameAsync(cancellationToken);
        var menuById = menus
            .GroupBy(menu => menu.MenuId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        var selectedFoodCosts = new List<decimal?>();

        foreach (var selection in request.Selections ?? Array.Empty<PackageCourseSelectionDto>())
        {
            menuById.TryGetValue(selection.MenuId, out var menu);
            selectedFoodCosts.Add(menu?.FoodCost);
        }

        var hasSelection = selectedFoodCosts.Count > 0;
        var selectedFoodCostPerPax = PackageCostingCalculator.SelectedFoodCostPerPax(selectedFoodCosts);
        var foodCostPerPax = PackageCostingCalculator.ResolvedFoodCostPerPax(
            hasSelection, selectedFoodCostPerPax, package.StandardRunFoodCostPerPax);

        var pax = request.Pax;
        var baseTotal = PackageCostingCalculator.BaseTotal(package.AmountPax, pax);

        // The extended total exists only when a municipality was selected (contract §13.7).
        var extendedTotal = area is null
            ? null
            : PackageCostingCalculator.ExtendedTotal(area.AdditionalRate, pax);

        var effectiveRatePerPax = PackageCostingCalculator.EffectiveRatePerPax(
            package.AmountPax, area?.AdditionalRate);

        var totalContractValue = PackageCostingCalculator.TotalContractValue(
            package.AmountPax, area?.AdditionalRate, pax);

        var foodCostTotal = PackageCostingCalculator.FoodCostTotal(foodCostPerPax, pax);
        var logisticsCost = area?.LogisticsCost;

        var grossProfit = PackageCostingCalculator.GrossProfitWithLogistics(
            totalContractValue, foodCostTotal, logisticsCost);

        var preview = new PackageLocationPreviewDto
        {
            PackageId = package.PackageId,
            Pax = pax,
            AmountPax = package.AmountPax,
            AreaId = area?.AreaId,
            AreaName = area?.AreaDetails,
            // The rate itself stays null when the municipality has stated none (DEC-086).
            AdditionalRate = area?.AdditionalRate,
            BaseTotal = baseTotal,
            ExtendedTotal = extendedTotal,
            EffectiveRatePerPax = effectiveRatePerPax,
            TotalContractValue = totalContractValue,
            FoodCostPerPax = foodCostPerPax,
            FoodCostTotal = foodCostTotal,
            LogisticsCost = logisticsCost,
            GrossProfit = grossProfit,
            GrossProfitPercentage = PackageCostingCalculator.GrossProfitPercentage(
                grossProfit, totalContractValue),
            GeneratedAt = _clock.UtcNow
        };

        return Result<PackageLocationPreviewDto>.Success(preview);
    }
}
