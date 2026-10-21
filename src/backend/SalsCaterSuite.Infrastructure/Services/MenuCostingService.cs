using SalsCaterSuite.Application.Common.Interfaces;
using SalsCaterSuite.Application.Common.Models;
using SalsCaterSuite.Application.Features.Menus.DTOs;
using SalsCaterSuite.Application.Features.Menus.Services;

namespace SalsCaterSuite.Infrastructure.Services;

/// <summary>
/// Menu-costing service (DEC-037). Reads menus through <see cref="IMenuRepository"/>; it
/// contains no EF Core reference. The costing arithmetic is unchanged.
/// <para>
/// The target food-cost percentage is no longer hardcoded: it is read from the deployment
/// configuration through <see cref="IFoodCostBenchmarkProvider"/>, the same source the
/// operations dashboard uses, so the menu screen and the dashboard can never disagree
/// (DEC-040, AC-B8).
/// </para>
/// </summary>
public class MenuCostingService : IMenuCostingService
{
    private readonly IMenuRepository _menus;
    private readonly IFoodCostBenchmarkProvider _benchmarks;

    /// <param name="menus">The menu repository.</param>
    /// <param name="benchmarks">
    /// The configured food-cost benchmark. The parameter is optional so the service can still be
    /// constructed without a configuration provider; in that case the BR-07 default of 35 % is
    /// used, which is the behaviour the service had before the benchmark became configurable.
    /// </param>
    public MenuCostingService(IMenuRepository menus, IFoodCostBenchmarkProvider? benchmarks = null)
    {
        _menus = menus;
        _benchmarks = benchmarks ?? DefaultFoodCostBenchmarkProvider.Instance;
    }

    public async Task<Result<MenuCostingSummaryDto>> CalculateCostingAsync(string menuId, CancellationToken cancellationToken = default)
    {
        var menu = await _menus.GetByIdAsync(menuId, cancellationToken);

        if (menu == null)
        {
            return Result<MenuCostingSummaryDto>.Failure("Menu dish not found.");
        }

        var sellingPrice = menu.Price ?? 0m;
        var foodCost = menu.FoodCost ?? 0m;
        var grossProfit = MenuCostingCalculator.GrossProfit(sellingPrice, foodCost);
        var foodCostPct = MenuCostingCalculator.FoodCostPercentage(sellingPrice, foodCost);
        var grossMarginPct = MenuCostingCalculator.GrossMarginPercentage(sellingPrice, foodCost);

        var dto = new MenuCostingSummaryDto
        {
            MenuId = menu.MenuId,
            MenuName = menu.MenuName,
            SellingPrice = sellingPrice,
            FoodCost = foodCost,
            GrossProfit = grossProfit,
            FoodCostPercentage = foodCostPct,
            GrossMarginPercentage = grossMarginPct,
            TargetFoodCostPercentage = _benchmarks.Current.TargetFoodCostPercentage
        };

        return Result<MenuCostingSummaryDto>.Success(dto);
    }

    public async Task<Result<List<MenuCostingSummaryDto>>> GetAllCostingsAsync(CancellationToken cancellationToken = default)
    {
        var menus = await _menus.GetOrderedByNameAsync(cancellationToken);
        var targetFoodCostPercentage = _benchmarks.Current.TargetFoodCostPercentage;

        var list = menus.Select(m =>
        {
            var sellingPrice = m.Price ?? 0m;
            var foodCost = m.FoodCost ?? 0m;
            var grossProfit = MenuCostingCalculator.GrossProfit(sellingPrice, foodCost);
            var foodCostPct = MenuCostingCalculator.FoodCostPercentage(sellingPrice, foodCost);
            var grossMarginPct = MenuCostingCalculator.GrossMarginPercentage(sellingPrice, foodCost);

            return new MenuCostingSummaryDto
            {
                MenuId = m.MenuId,
                MenuName = m.MenuName,
                SellingPrice = sellingPrice,
                FoodCost = foodCost,
                GrossProfit = grossProfit,
                FoodCostPercentage = foodCostPct,
                GrossMarginPercentage = grossMarginPct,
                TargetFoodCostPercentage = targetFoodCostPercentage
            };
        }).ToList();

        return Result<List<MenuCostingSummaryDto>>.Success(list);
    }
}
