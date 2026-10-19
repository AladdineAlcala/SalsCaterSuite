using SalsCaterSuite.Application.Common.Interfaces;
using SalsCaterSuite.Application.Common.Models;
using SalsCaterSuite.Application.Features.Menus.DTOs;

namespace SalsCaterSuite.Application.Features.Menus.Queries;

/// <summary>GET /api/menus/{id} — a single menu item.</summary>
public class GetMenuByIdQuery
{
    private readonly IMenuRepository _menus;

    public GetMenuByIdQuery(IMenuRepository menus)
    {
        _menus = menus;
    }

    public async Task<Result<MenuDto>> ExecuteAsync(string id, CancellationToken cancellationToken = default)
    {
        var menu = await _menus.GetProjectionByIdAsync(id, cancellationToken);

        return menu is null
            ? Result<MenuDto>.Failure("Menu item not found.")
            : Result<MenuDto>.Success(menu);
    }
}
