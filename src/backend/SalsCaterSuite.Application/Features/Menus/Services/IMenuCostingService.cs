using SalsCaterSuite.Application.Common.Models;
using SalsCaterSuite.Application.Features.Menus.DTOs;

namespace SalsCaterSuite.Application.Features.Menus.Services;

public interface IMenuCostingService
{
    Task<Result<MenuCostingSummaryDto>> CalculateCostingAsync(string menuId, CancellationToken cancellationToken = default);
    Task<Result<List<MenuCostingSummaryDto>>> GetAllCostingsAsync(CancellationToken cancellationToken = default);
}

