using SalsCaterSuite.Application.Common.Interfaces;
using SalsCaterSuite.Application.Common.Models;
using SalsCaterSuite.Application.Features.Menus.DTOs;

namespace SalsCaterSuite.Application.Features.Menus.Queries;

/// <summary>GET /api/menus — list menu items, optionally filtered by course.</summary>
public class GetMenusQuery
{
    private readonly IMenuRepository _menus;

    public GetMenusQuery(IMenuRepository menus)
    {
        _menus = menus;
    }

    public async Task<Result<List<MenuDto>>> ExecuteAsync(int? courseId, CancellationToken cancellationToken = default)
    {
        var menus = await _menus.GetProjectionListAsync(courseId, cancellationToken);

        return Result<List<MenuDto>>.Success(menus);
    }
}
