using SalsCaterSuite.Application.Common.Interfaces;
using SalsCaterSuite.Application.Common.Models;
using SalsCaterSuite.Application.Features.Bookings.DTOs.Request;
using SalsCaterSuite.Application.Features.Bookings.DTOs.Response;
using SalsCaterSuite.Application.Features.Bookings.Models;

namespace SalsCaterSuite.Application.Features.Bookings.Queries;

/// <summary>
/// <c>GET /api/addons/categories</c> — the read-only add-on category master
/// (contract §16.2, RSK-066).
/// </summary>
public class GetAddonCategoriesQuery
{
    private readonly IAddonRepository _addons;

    public GetAddonCategoriesQuery(IAddonRepository addons)
    {
        _addons = addons;
    }

    public async Task<Result<List<AddonCategoryResponse>>> ExecuteAsync(
        CancellationToken cancellationToken = default)
    {
        var rows = await _addons.GetCategoriesAsync(cancellationToken);

        return Result<List<AddonCategoryResponse>>.Success(rows
            .Select(category => new AddonCategoryResponse
            {
                AddonCategoryId = category.AddonCategoryId,
                AddonCategoryName = category.AddonCategoryName
            })
            .ToList());
    }
}
