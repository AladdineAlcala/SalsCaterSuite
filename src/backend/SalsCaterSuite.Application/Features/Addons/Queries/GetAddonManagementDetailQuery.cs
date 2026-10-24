using SalsCaterSuite.Application.Common.Interfaces;
using SalsCaterSuite.Application.Common.Models;
using SalsCaterSuite.Application.Features.Addons.DTOs;

namespace SalsCaterSuite.Application.Features.Addons.Queries;

/// <summary>
/// <c>GET /api/addons/manage/{id}</c> — one non-soft-deleted add-on for the administration form.
/// A missing or soft-deleted row is an <see cref="AddonNotFoundResult{AddonAdminItemDto}"/> so the
/// API answers <c>404</c> by result type.
/// </summary>
public class GetAddonManagementDetailQuery
{
    private readonly IAddonRepository _addons;

    public GetAddonManagementDetailQuery(IAddonRepository addons)
    {
        _addons = addons;
    }

    public async Task<Result<AddonAdminItemDto>> ExecuteAsync(
        int addonId,
        CancellationToken cancellationToken = default)
    {
        var addon = await _addons.GetByIdAsync(addonId, cancellationToken);

        return addon is null
            ? new AddonNotFoundResult<AddonAdminItemDto>("Add-on not found.")
            : Result<AddonAdminItemDto>.Success(AddonAdminMapper.ToDto(addon));
    }
}
