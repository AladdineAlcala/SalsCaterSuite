using SalsCaterSuite.Application.Common.Interfaces;
using SalsCaterSuite.Application.Common.Models;
using SalsCaterSuite.Application.Features.Menus.DTOs.Request;
using SalsCaterSuite.Application.Features.Menus.DTOs.Response;
using SalsCaterSuite.Application.Features.Menus.Models;

namespace SalsCaterSuite.Application.Features.Menus.Queries;

/// <summary>GET /api/menus/{id} — a single menu item.</summary>
public class GetMenuByIdQuery
{
    private readonly IMenuRepository _menus;

    public GetMenuByIdQuery(IMenuRepository menus)
    {
        _menus = menus;
    }

    public async Task<Result<MenuResponse>> ExecuteAsync(string id, CancellationToken cancellationToken = default)
    {
        var menu = await _menus.GetProjectionByIdAsync(id, cancellationToken);

        return menu is null
            ? Result<MenuResponse>.Failure("Menu item not found.")
            : Result<MenuResponse>.Success(menu);
    }
}
