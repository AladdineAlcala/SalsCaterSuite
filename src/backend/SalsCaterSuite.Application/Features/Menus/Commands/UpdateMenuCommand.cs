using SalsCaterSuite.Application.Common.Interfaces;
using SalsCaterSuite.Application.Common.Models;
using SalsCaterSuite.Application.Features.Menus.DTOs;

namespace SalsCaterSuite.Application.Features.Menus.Commands;

/// <summary>PUT /api/menus/{id} — update a menu item.</summary>
public class UpdateMenuCommand
{
    private readonly IMenuRepository _menus;

    public UpdateMenuCommand(IMenuRepository menus)
    {
        _menus = menus;
    }

    public async Task<Result> ExecuteAsync(string id, CreateMenuRequestDto request, CancellationToken cancellationToken = default)
    {
        var menu = await _menus.GetByIdAsync(id, cancellationToken);
        if (menu is null)
        {
            return Result.Failure("Menu item not found.");
        }

        menu.CourseId = request.CourseId;
        menu.MenuName = request.MenuName;
        menu.Note = request.Note;
        menu.Price = request.Price;
        menu.FoodCost = request.FoodCost;

        await _menus.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
