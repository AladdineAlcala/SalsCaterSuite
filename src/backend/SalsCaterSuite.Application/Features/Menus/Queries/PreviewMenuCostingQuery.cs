using FluentValidation;
using SalsCaterSuite.Application.Common.Interfaces;
using SalsCaterSuite.Application.Common.Models;
using SalsCaterSuite.Application.Features.Menus.DTOs;
using SalsCaterSuite.Application.Features.Menus.Services;

namespace SalsCaterSuite.Application.Features.Menus.Queries;

/// <summary>
/// POST /api/menus/costing/preview — the server-computed margin preview (spec §6.2, AC-M15).
/// <para>
/// The preview is computed here, through the same <see cref="MenuCostingCalculator"/> the saved
/// dish and the menu master use, so the preview and the saved record can never disagree
/// (DEC-002).
/// </para>
/// </summary>
public class PreviewMenuCostingQuery
{
    private readonly IValidator<MenuCostingPreviewRequestDto> _validator;
    private readonly IFoodCostBenchmarkProvider _benchmarks;

    public PreviewMenuCostingQuery(
        IValidator<MenuCostingPreviewRequestDto> validator,
        IFoodCostBenchmarkProvider benchmarks)
    {
        _validator = validator;
        _benchmarks = benchmarks;
    }

    /// <summary>
    /// The preview of a selling price and a food cost against the configured benchmark. A
    /// rejected payload returns the validation messages, which the API renders as
    /// <c>400 { "errors": [ ... ] }</c>.
    /// </summary>
    public async Task<Result<MenuCostingPreviewDto>> ExecuteAsync(
        MenuCostingPreviewRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var validationResult = await _validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            return Result<MenuCostingPreviewDto>.Failure(
                validationResult.Errors.Select(e => e.ErrorMessage).ToArray());
        }

        var benchmark = _benchmarks.Current;
        var sellingPrice = request.SellingPrice;
        var foodCost = request.FoodCost;
        var foodCostPercentage = MenuCostingCalculator.FoodCostPercentage(sellingPrice, foodCost);

        return Result<MenuCostingPreviewDto>.Success(new MenuCostingPreviewDto
        {
            SellingPrice = sellingPrice,
            FoodCost = foodCost,
            GrossProfit = MenuCostingCalculator.GrossProfit(sellingPrice, foodCost),
            FoodCostPercentage = foodCostPercentage,
            GrossMarginPercentage = MenuCostingCalculator.GrossMarginPercentage(sellingPrice, foodCost),
            TargetFoodCostPercentage = benchmark.TargetFoodCostPercentage,
            TargetMarginPercentage = benchmark.TargetMarginPercentage,
            IsWithinTarget = foodCostPercentage <= benchmark.TargetFoodCostPercentage,
            MarginVariancePercentage = foodCostPercentage - benchmark.TargetFoodCostPercentage
        });
    }
}
