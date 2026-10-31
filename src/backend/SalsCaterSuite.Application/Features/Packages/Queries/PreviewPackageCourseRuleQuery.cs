using FluentValidation;
using SalsCaterSuite.Application.Common.Interfaces;
using SalsCaterSuite.Application.Common.Models;
using SalsCaterSuite.Application.Features.Packages.DTOs.Request;
using SalsCaterSuite.Application.Features.Packages.DTOs.Response;
using SalsCaterSuite.Application.Features.Packages.Models;
using SalsCaterSuite.Application.Features.Packages.Services;

namespace SalsCaterSuite.Application.Features.Packages.Queries;

/// <summary>
/// POST /api/packages/{id}/course-rules/preview — the selection simulator and the headcount
/// forecast (contract §13.3, REQ-PKG-02).
/// <para>
/// A preview is a <b>what-if</b>: it persists nothing, it changes no price and it is not a
/// booking total (BR-20). Every figure is computed on the server through
/// <see cref="PackageCostingCalculator"/> (DEC-002).
/// </para>
/// <para>
/// The two verdicts are published separately (DEC-080): <c>isQuotaCompliant</c> is the quota
/// verdict alone and <c>isCompliant</c> is the overall verdict — the quota verdict <b>and</b> the
/// absence of a cost-ceiling breach. A breach warns; it never reprices and it never blocks.
/// </para>
/// </summary>
public class PreviewPackageCourseRuleQuery
{
    private readonly GetPackageCourseRulesetQuery _ruleset;
    private readonly IMenuRepository _menus;
    private readonly IValidator<PackageCourseRulePreviewRequest> _validator;
    private readonly IDateTime _clock;

    public PreviewPackageCourseRuleQuery(
        GetPackageCourseRulesetQuery ruleset,
        IMenuRepository menus,
        IValidator<PackageCourseRulePreviewRequest> validator,
        IDateTime clock)
    {
        _ruleset = ruleset;
        _menus = menus;
        _validator = validator;
        _clock = clock;
    }

    public async Task<Result<PackageCourseRulePreviewResponse>> ExecuteAsync(
        int packageId,
        PackageCourseRulePreviewRequest request,
        CancellationToken cancellationToken = default)
    {
        var validationResult = await _validator.ValidateAsync(request, cancellationToken);

        if (!validationResult.IsValid)
        {
            return Result<PackageCourseRulePreviewResponse>.Failure(
                validationResult.Errors.Select(error => error.ErrorMessage).ToArray());
        }

        var ruleset = await _ruleset.ExecuteAsync(packageId, cancellationToken);

        if (!ruleset.Succeeded)
        {
            return new PackageNotFoundResult<PackageCourseRulePreviewResponse>(
                ruleset.Errors.FirstOrDefault() ?? "Package not found.");
        }

        var package = ruleset.Data!;

        var menus = await _menus.GetOrderedByNameAsync(cancellationToken);
        var menuById = menus
            .GroupBy(menu => menu.MenuId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        var courses = await _menus.GetCourseProjectionsAsync(cancellationToken);
        var courseNameById = courses.ToDictionary(
            course => course.CourseId,
            course => course.CourseName ?? string.Empty);

        var selections = new List<PackageCourseSelectionResultResponse>();
        var selectedFoodCosts = new List<decimal?>();

        foreach (var selection in request.Selections ?? Array.Empty<PackageCourseSelectionRequest>())
        {
            menuById.TryGetValue(selection.MenuId, out var menu);

            selections.Add(new PackageCourseSelectionResultResponse
            {
                CourseId = selection.CourseId,
                CourseName = courseNameById.TryGetValue(selection.CourseId, out var courseName)
                    ? courseName
                    : string.Empty,
                MenuId = selection.MenuId,
                MenuName = menu?.MenuName ?? string.Empty,
                FoodCost = menu?.FoodCost
            });

            selectedFoodCosts.Add(menu?.FoodCost);
        }

        var hasSelection = selections.Count > 0;

        // A dish that is not costed makes the selection's food cost unknown — never zero
        // (DEC-087). The quota verdict still counts the dish.
        var selectedFoodCostPerPax = PackageCostingCalculator.SelectedFoodCostPerPax(selectedFoodCosts);
        var foodCostPerPax = PackageCostingCalculator.ResolvedFoodCostPerPax(
            hasSelection, selectedFoodCostPerPax, package.StandardRunFoodCostPerPax);

        var costCeilingPerPax = PackageCostingCalculator.CostCeilingPerPax(
            package.AmountPax, package.TargetFoodCostPercentage);
        var isCeilingBreached = PackageCostingCalculator.IsCeilingBreached(
            selectedFoodCostPerPax, costCeilingPerPax);

        var compliance = BuildCompliance(package, selections, courseNameById);        var isQuotaCompliant = compliance.All(entry => entry.IsCompliant);

        // The resolved headcount: the requested one, otherwise the tier's floor, otherwise null.
        var pax = request.Pax ?? package.MinPax;

        var grossRevenue = PackageCostingCalculator.GrossRevenue(package.AmountPax, pax);
        var foodCostTotal = PackageCostingCalculator.FoodCostTotal(foodCostPerPax, pax);
        var grossProfit = PackageCostingCalculator.GrossProfit(grossRevenue, foodCostTotal);

        var stressedFoodCostPerPax = PackageCostingCalculator.StressedFoodCostPerPax(
            selectedFoodCostPerPax, request.StressPercentage);

        var preview = new PackageCourseRulePreviewResponse
        {
            PackageId = package.PackageId,
            AmountPax = package.AmountPax,
            MinPax = package.MinPax,
            Pax = pax,
            TargetFoodCostPercentage = package.TargetFoodCostPercentage,
            CostCeilingPerPax = costCeilingPerPax,
            SelectedFoodCostPerPax = selectedFoodCostPerPax,
            FoodCostPerPax = foodCostPerPax,
            EffectiveFoodCostPercentage = PackageCostingCalculator.EffectiveFoodCostPercentage(
                selectedFoodCostPerPax, package.AmountPax),
            IsCostKnown = selectedFoodCostPerPax.HasValue,
            IsCeilingBreached = isCeilingBreached,
            CeilingShortfallPerPax = PackageCostingCalculator.CeilingShortfallPerPax(
                selectedFoodCostPerPax, costCeilingPerPax),
            CeilingOveragePercentage = PackageCostingCalculator.CeilingOveragePercentage(
                selectedFoodCostPerPax, costCeilingPerPax),
            StressPercentage = request.StressPercentage,
            StressedFoodCostPerPax = stressedFoodCostPerPax,
            StressedFoodCostPercentage = PackageCostingCalculator.StressedFoodCostPercentage(
                stressedFoodCostPerPax, package.AmountPax),
            GrossRevenue = grossRevenue,
            FoodCostTotal = foodCostTotal,
            GrossProfit = grossProfit,
            GrossProfitPercentage = PackageCostingCalculator.GrossProfitPercentage(
                grossProfit, grossRevenue),
            IsQuotaCompliant = isQuotaCompliant,
            IsCompliant = isQuotaCompliant && !isCeilingBreached,
            Compliance = compliance,
            Selections = selections,
            GeneratedAt = _clock.UtcNow
        };

        return Result<PackageCourseRulePreviewResponse>.Success(preview);
    }

    /// <summary>
    /// The compliance list: one entry per rule, plus one per <b>unruled</b> course the selection
    /// names. A rule is satisfied when the selected count is <b>exactly</b> its quota (BR-15).
    /// <para>
    /// It delegates to the <b>one</b> shared evaluator, so the simulator and the booking's own
    /// selection surface can never disagree about whether a selection complies.
    /// </para>
    /// </summary>
    private static List<PackageCourseComplianceResponse> BuildCompliance(
        PackageCourseRulesetResponse package,
        IReadOnlyCollection<PackageCourseSelectionResultResponse> selections,
        IReadOnlyDictionary<int, string> courseNameById) =>
        PackageSelectionCompliance.Evaluate(
            package.Rules.Select(rule => (rule.CourseId, rule.CourseName, rule.RequiredQuota)),
            courseNameById,
            selections.Select(selection => (selection.CourseId, selection.MenuId)));
}
