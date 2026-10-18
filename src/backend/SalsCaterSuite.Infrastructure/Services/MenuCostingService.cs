using Microsoft.EntityFrameworkCore;
using SalsCaterSuite.Application.Common.Interfaces;
using SalsCaterSuite.Application.Common.Models;
using SalsCaterSuite.Application.Features.Menus.DTOs;
using SalsCaterSuite.Application.Features.Menus.Services;

namespace SalsCaterSuite.Infrastructure.Services;

public class MenuCostingService : IMenuCostingService
{
    private readonly ISalsCaterSuiteDbContext _context;

    public MenuCostingService(ISalsCaterSuiteDbContext context)
    {
        _context = context;
    }

    public async Task<Result<MenuCostingSummaryDto>> CalculateCostingAsync(string menuId, CancellationToken cancellationToken = default)
    {
        var menu = await _context.Menus
            .FirstOrDefaultAsync(m => m.MenuId == menuId, cancellationToken);

        if (menu == null)
        {
            return Result<MenuCostingSummaryDto>.Failure("Menu dish not found.");
        }

        var sellingPrice = menu.Price ?? 0m;
        var foodCost = menu.FoodCost ?? 0m;
        var grossProfit = sellingPrice - foodCost;
        var foodCostPct = sellingPrice > 0 ? Math.Round((foodCost / sellingPrice) * 100m, 2) : 0m;

        var dto = new MenuCostingSummaryDto
        {
            MenuId = menu.MenuId,
            MenuName = menu.MenuName,
            SellingPrice = sellingPrice,
            FoodCost = foodCost,
            GrossProfit = grossProfit,
            FoodCostPercentage = foodCostPct,
            TargetFoodCostPercentage = 35.0m
        };

        return Result<MenuCostingSummaryDto>.Success(dto);
    }

    public async Task<Result<List<MenuCostingSummaryDto>>> GetAllCostingsAsync(CancellationToken cancellationToken = default)
    {
        var menus = await _context.Menus
            .OrderBy(m => m.MenuName)
            .ToListAsync(cancellationToken);

        var list = menus.Select(m =>
        {
            var sellingPrice = m.Price ?? 0m;
            var foodCost = m.FoodCost ?? 0m;
            var grossProfit = sellingPrice - foodCost;
            var foodCostPct = sellingPrice > 0 ? Math.Round((foodCost / sellingPrice) * 100m, 2) : 0m;

            return new MenuCostingSummaryDto
            {
                MenuId = m.MenuId,
                MenuName = m.MenuName,
                SellingPrice = sellingPrice,
                FoodCost = foodCost,
                GrossProfit = grossProfit,
                FoodCostPercentage = foodCostPct,
                TargetFoodCostPercentage = 35.0m
            };
        }).ToList();

        return Result<List<MenuCostingSummaryDto>>.Success(list);
    }
}

