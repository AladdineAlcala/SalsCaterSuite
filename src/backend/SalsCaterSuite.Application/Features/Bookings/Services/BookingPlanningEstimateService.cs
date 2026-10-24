using SalsCaterSuite.Application.Common.Interfaces;
using SalsCaterSuite.Application.Features.Packages.Services;
using SalsCaterSuite.Domain.Entities;

namespace SalsCaterSuite.Application.Features.Bookings.Services;

/// <summary>
/// The <b>per-booking</b> planning estimate of the register (contract §14.1, DEC-132).
/// <para>
/// The published <c>baseGrossProfitPercentage</c> is the <b>selection's</b> estimate when the
/// booking carries a live selection, and the <b>tier's standard run</b> otherwise. It is a
/// planning figure — the dish food costs are manually entered (DEC-062) and the recipe engine
/// is unbuilt (REQ-MNU-03) — and it never enters a booking total.
/// </para>
/// <para>
/// The tier's standard run is computed <b>once per distinct tier</b> (RSK-054) through the one
/// <see cref="BookingTierEstimateService"/>; the catalogue is read <b>once</b> for the request.
/// </para>
/// </summary>
public class BookingPlanningEstimateService
{
    private readonly BookingTierEstimateService _tierEstimates;
    private readonly IMenuRepository _menus;

    public BookingPlanningEstimateService(BookingTierEstimateService tierEstimates, IMenuRepository menus)
    {
        _tierEstimates = tierEstimates;
        _menus = menus;
    }

    /// <summary>
    /// The base gross profit percentage of each booking, keyed by <c>TransactionId</c>.
    /// <c>null</c> when neither the selection nor the tier provides a known cost.
    /// </summary>
    public async Task<IReadOnlyDictionary<int, decimal?>> GetBaseGrossProfitPercentagesAsync(
        IReadOnlyList<Booking> bookings,
        CancellationToken cancellationToken = default)
    {
        var result = new Dictionary<int, decimal?>();

        if (bookings.Count == 0)
        {
            return result;
        }

        var tiers = await _tierEstimates.GetAsync(
            bookings
                .Where(booking => booking.PackageId.HasValue)
                .Select(booking => booking.PackageId!.Value),
            cancellationToken);

        // The catalogue is read once, whatever the number of bookings or selections.
        var foodCostByMenuId = (await _menus.GetOrderedByNameAsync(cancellationToken))
            .GroupBy(menu => menu.MenuId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.First().FoodCost,
                StringComparer.OrdinalIgnoreCase);

        foreach (var booking in bookings)
        {
            var liveSelection = booking.Menus.Where(menu => !menu.IsDeleted).ToList();

            decimal? percentage;

            if (liveSelection.Count > 0)
            {
                // The selection's own cost. One uncosted dish makes it unknown — never zero
                // (DEC-087), and the tier's standard run is NOT substituted (it is a different
                // figure and showing it would mislead).
                var selectedFoodCost = PackageCostingCalculator.SelectedFoodCostPerPax(
                    liveSelection.Select(row => foodCostByMenuId.TryGetValue(row.MenuId, out var cost)
                        ? cost
                        : null));

                percentage = PackageCostingCalculator.BaseGrossProfitPercentage(
                    booking.PricePerHead, selectedFoodCost);
            }
            else
            {
                percentage = booking.PackageId.HasValue
                    && tiers.TryGetValue(booking.PackageId.Value, out var tier)
                        ? tier.BaseGrossProfitPercentage
                        : null;
            }

            result[booking.TransactionId] = percentage;
        }

        return result;
    }
}
