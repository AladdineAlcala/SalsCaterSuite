using SalsCaterSuite.Application.Common.Interfaces;
using SalsCaterSuite.Application.Common.Models;

namespace SalsCaterSuite.Application.Features.Menus.Commands;

/// <summary>
/// DELETE /api/menus/{id} — delete a menu item.
/// <para>
/// The delete is a <b>soft delete</b> (DEC-063): the dish is flagged and its audit stamp is
/// refreshed, never physically destroyed. The kitchen's dish master is a record of what was
/// cooked and what it cost; a row that disappears cannot be reconstructed, and the master
/// query already excludes the flagged rows.
/// </para>
/// <para>
/// The flag also activates the <c>IsDeleted == false</c> exclusion the menu feature's own
/// reads apply (DEC-045, DEC-050) — before this correction nothing could ever set the flag,
/// so that exclusion was unreachable through the API.
/// </para>
/// </summary>
public class DeleteMenuCommand
{
    private readonly IMenuRepository _menus;

    public DeleteMenuCommand(IMenuRepository menus)
    {
        _menus = menus;
    }

    public async Task<Result> ExecuteAsync(string id, CancellationToken cancellationToken = default)
    {
        // A soft-deleted dish is not found: the read excludes the flagged rows (DEC-063), so
        // deleting the same dish twice reports "not found" rather than stamping it twice.
        var menu = await _menus.GetByIdAsync(id, cancellationToken);
        if (menu is null)
        {
            return Result.Failure("Menu item not found.");
        }

        menu.IsDeleted = true;
        menu.UpdatedDate = DateTime.UtcNow;

        await _menus.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
