using SalsCaterSuite.Application.Common.Interfaces;
using SalsCaterSuite.Application.Common.Models;
using SalsCaterSuite.Application.Features.Bookings.DTOs;
using SalsCaterSuite.Application.Services;

namespace SalsCaterSuite.Application.Features.Bookings.Queries;

/// <summary>
/// <c>GET /api/addons</c> — the read-only add-on master (contract §16.1, RSK-066).
/// <para>
/// Only the <b>active</b> rows are returned. <c>lineAmountForBooking</c> is published so the
/// client never performs the multiplication itself (DEC-158).
/// </para>
/// <para>
/// <b>Contract ambiguity (documented in the implementation report):</b> §16.1 omits the
/// headcount that <c>lineAmountForBooking</c> is derived from and does not name a query
/// parameter for it. The endpoint accepts an optional <c>headcount</c> (default <c>1</c>), so a
/// <c>PerGuest</c> add-on's default line total is the unit price when no headcount is supplied.
/// </para>
/// </summary>
public class GetAddonMasterQuery
{
    private readonly IAddonRepository _addons;

    public GetAddonMasterQuery(IAddonRepository addons)
    {
        _addons = addons;
    }

    public async Task<Result<List<AddonMasterItemDto>>> ExecuteAsync(
        int? categoryId,
        int headcount = 1,
        CancellationToken cancellationToken = default)
    {
        // A headcount of zero or less is meaningless for a per-guest line: one head is the safe
        // default, exactly as the missing parameter is.
        var heads = headcount > 0 ? headcount : 1;

        var rows = await _addons.GetActiveWithCategoryAsync(categoryId, cancellationToken);

        return Result<List<AddonMasterItemDto>>.Success(rows
            .Select(addon => new AddonMasterItemDto
            {
                AddonId = addon.AddonId,
                Description = addon.Description,
                Amount = addon.UnitPrice,
                Unit = addon.Unit,
                PricingBasis = addon.PricingBasis,
                LineAmountForBooking = BookingAmountDueCalculator.Round2(
                    addon.LineAmountFor(heads)),
                AddonCategoryId = addon.AddonCategoryId,
                AddonCategoryName = addon.AddonCategory?.AddonCategoryName,
                DeptId = addon.DeptId,
                DeptName = addon.Department?.DeptName,
                IsActive = addon.IsActive
            })
            .ToList());
    }
}
