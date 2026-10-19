using SalsCaterSuite.Application.Common.Interfaces;
using SalsCaterSuite.Application.Common.Models;
using SalsCaterSuite.Application.Features.Menus.DTOs;
using SalsCaterSuite.Domain.Entities;

namespace SalsCaterSuite.Application.Features.Menus.Commands;

/// <summary>
/// POST /api/menus — create a menu item. The only failure mode is a duplicate
/// id, which the API maps to HTTP 409 Conflict.
/// </summary>
public class CreateMenuCommand
{
    private readonly IMenuRepository _menus;

    public CreateMenuCommand(IMenuRepository menus)
    {
        _menus = menus;
    }

    public async Task<Result<string>> ExecuteAsync(CreateMenuRequestDto request, CancellationToken cancellationToken = default)
    {
        var exists = await _menus.ExistsAsync(request.MenuId, cancellationToken);
        if (exists)
        {
            return Result<string>.Failure($"Menu item '{request.MenuId}' already exists.");
        }

        var menu = new Menu
        {
            MenuId = request.MenuId.ToUpper(),
            CourseId = request.CourseId,
            MenuName = request.MenuName,
            Note = request.Note,
            Price = request.Price,
            FoodCost = request.FoodCost
        };

        await _menus.AddAsync(menu, cancellationToken);
        await _menus.SaveChangesAsync(cancellationToken);

        return Result<string>.Success(menu.MenuId);
    }
}
