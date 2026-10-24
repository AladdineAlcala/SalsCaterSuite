using SalsCaterSuite.Application.Common.Interfaces;
using SalsCaterSuite.Application.Common.Models;
using SalsCaterSuite.Application.Features.Bookings.DTOs;

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

    public async Task<Result<List<AddonCategoryDto>>> ExecuteAsync(
        CancellationToken cancellationToken = default)
    {
        var rows = await _addons.GetCategoriesAsync(cancellationToken);

        return Result<List<AddonCategoryDto>>.Success(rows
            .Select(category => new AddonCategoryDto
            {
                AddonCategoryId = category.AddonCategoryId,
                AddonCategoryName = category.AddonCategoryName
            })
            .ToList());
    }
}
