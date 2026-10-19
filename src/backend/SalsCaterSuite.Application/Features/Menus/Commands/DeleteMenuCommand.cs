using SalsCaterSuite.Application.Common.Interfaces;
using SalsCaterSuite.Application.Common.Models;

namespace SalsCaterSuite.Application.Features.Menus.Commands;

/// <summary>DELETE /api/menus/{id} — delete a menu item.</summary>
public class DeleteMenuCommand
{
    private readonly IMenuRepository _menus;

    public DeleteMenuCommand(IMenuRepository menus)
    {
        _menus = menus;
    }

    public async Task<Result> ExecuteAsync(string id, CancellationToken cancellationToken = default)
    {
        var menu = await _menus.GetByIdAsync(id, cancellationToken);
        if (menu is null)
        {
            return Result.Failure("Menu item not found.");
        }

        _menus.Remove(menu);
        await _menus.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
