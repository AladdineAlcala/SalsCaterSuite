using SalsCaterSuite.Application.Common.Interfaces;
using SalsCaterSuite.Application.Common.Models;
using SalsCaterSuite.Application.Features.Addons.DTOs.Request;
using SalsCaterSuite.Application.Features.Addons.DTOs.Response;

namespace SalsCaterSuite.Application.Features.Addons.Queries;

/// <summary>
/// <c>GET /api/addons/manage</c> — the add-on administration list (the owner's 2026-10-24
/// Settings → Add-ons request). It publishes <b>active and inactive</b> rows and excludes the
/// soft-deleted ones, so an administrator can see and reactivate a deactivated add-on.
/// </summary>
public class GetAddonManagementListQuery
{
    private readonly IAddonRepository _addons;

    public GetAddonManagementListQuery(IAddonRepository addons)
    {
        _addons = addons;
    }

    public async Task<Result<List<AddonAdminItemResponse>>> ExecuteAsync(
        CancellationToken cancellationToken = default)
    {
        var rows = await _addons.GetAllForManagementAsync(cancellationToken);

        return Result<List<AddonAdminItemResponse>>.Success(
            rows.Select(AddonAdminMapper.ToDto).ToList());
    }
}
