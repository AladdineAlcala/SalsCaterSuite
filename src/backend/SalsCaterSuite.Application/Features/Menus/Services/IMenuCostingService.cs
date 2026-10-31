using SalsCaterSuite.Application.Common.Models;
using SalsCaterSuite.Application.Features.Menus.DTOs.Request;
using SalsCaterSuite.Application.Features.Menus.DTOs.Response;
using SalsCaterSuite.Application.Features.Menus.Models;

namespace SalsCaterSuite.Application.Features.Menus.Services;

public interface IMenuCostingService
{
    Task<Result<MenuCostingSummaryResponse>> CalculateCostingAsync(string menuId, CancellationToken cancellationToken = default);
    Task<Result<List<MenuCostingSummaryResponse>>> GetAllCostingsAsync(CancellationToken cancellationToken = default);
}

