using SalsCaterSuite.Application.Common.Interfaces;
using SalsCaterSuite.Application.Common.Models;

namespace SalsCaterSuite.Application.Features.Addons.Commands;

/// <summary>
/// <c>DELETE /api/addons/{id}</c> — soft-delete an add-on (the owner's 2026-10-24 Settings →
/// Add-ons request). The row is flagged and its audit stamp refreshed, never physically
/// destroyed, so a booking's frozen line keeps referring to a real catalogue row.
/// </summary>
public class DeleteAddonCommand
{
    private readonly IAddonRepository _addons;

    public DeleteAddonCommand(IAddonRepository addons)
    {
        _addons = addons;
    }

    public async Task<Result> ExecuteAsync(int addonId, CancellationToken cancellationToken = default)
    {
        // A soft-deleted add-on is not found: the read excludes the flagged rows, so deleting the
        // same add-on twice reports "not found" rather than stamping it twice.
        var addon = await _addons.GetByIdAsync(addonId, cancellationToken);
        if (addon is null)
        {
            return new AddonNotFoundResult("Add-on not found.");
        }

        addon.IsDeleted = true;
        addon.UpdatedDate = DateTime.UtcNow;

        await _addons.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
