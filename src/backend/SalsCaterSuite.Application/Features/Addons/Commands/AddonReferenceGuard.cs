using SalsCaterSuite.Application.Common.Interfaces;
using SalsCaterSuite.Application.Features.Addons.DTOs.Request;
using SalsCaterSuite.Application.Features.Addons.DTOs.Response;

namespace SalsCaterSuite.Application.Features.Addons.Commands;

/// <summary>
/// The shared reference check of the add-on writes: an optional category and an optional
/// department must exist and be live. Returning the message from one place keeps the create and
/// the update wording identical.
/// </summary>
internal static class AddonReferenceGuard
{
    public static async Task<string?> CheckAsync(
        IAddonRepository addons,
        IDepartmentRepository departments,
        AddonUpsertRequest request,
        CancellationToken cancellationToken)
    {
        if (request.AddonCategoryId.HasValue
            && !await addons.CategoryExistsAsync(request.AddonCategoryId.Value, cancellationToken))
        {
            return "The add-on category does not exist.";
        }

        if (request.DeptId.HasValue
            && !await departments.ExistsAsync(request.DeptId.Value, cancellationToken))
        {
            return "The department does not exist.";
        }

        return null;
    }
}
