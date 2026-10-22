using SalsCaterSuite.Application.Common.Interfaces;
using SalsCaterSuite.Application.Features.Packages.Services;

namespace SalsCaterSuite.Application.Features.Bookings.Services;

/// <summary>
/// The planning estimate of one tier (DEC-103, BR-19).
/// <para>
/// These are <b>planning estimates</b>, never a per-booking margin: the model has no
/// <c>Booking</c> ↔ <c>Menu</c> link and no recipe/yield engine (REQ-MNU-02 is blocked,
/// DEC-059), so the true cost of a booked event is unknown. The tier's standard run — the mean
/// of each course rule's qualified, costed pool, summed — is the best estimate the model
/// carries.
/// </para>
/// </summary>
public sealed record BookingTierEstimate(
    int PackageId,
    decimal? AmountPax,
    int? MinPax,
    decimal? StandardRunFoodCostPerPax,
    decimal? BaseGrossProfitPercentage,
    int CourseRuleCount);

/// <summary>
/// The one service that produces a tier's planning estimate (DEC-103).
/// <para>
/// The estimate is computed <b>once per distinct tier</b>, not once per booking row (RSK-054),
/// and the menu catalogue is read <b>once</b> for the whole request. The arithmetic is the
/// shared <see cref="PackageCostingCalculator"/>, so the register's estimate and the packages
/// workbench's estimate can never disagree.
/// </para>
/// </summary>
public class BookingTierEstimateService
{
    private readonly IPackageRepository _packages;
    private readonly IPackageCourseRuleRepository _rules;
    private readonly IMenuRepository _menus;

    public BookingTierEstimateService(
        IPackageRepository packages,
        IPackageCourseRuleRepository rules,
        IMenuRepository menus)
    {
        _packages = packages;
        _rules = rules;
        _menus = menus;
    }

    /// <summary>
    /// The estimates of the given tiers. A tier that does not exist, that has no course rule,
    /// or whose pools contain no costed dish yields <c>null</c> planning figures — unknown,
    /// never zero (BR-20).
    /// </summary>
    public async Task<IReadOnlyDictionary<int, BookingTierEstimate>> GetAsync(
        IEnumerable<int> packageIds,
        CancellationToken cancellationToken = default)
    {
        var distinct = packageIds.Distinct().ToList();

        if (distinct.Count == 0)
        {
            return new Dictionary<int, BookingTierEstimate>();
        }

        // The menu catalogue is read once for the whole request.
        var menus = await _menus.GetOrderedByNameAsync(cancellationToken);
        var pools = menus
            .Where(menu => menu.CourseId.HasValue)
            .GroupBy(menu => menu.CourseId!.Value)
            .ToDictionary(group => group.Key, group => group.ToList());

        var estimates = new Dictionary<int, BookingTierEstimate>();

        foreach (var packageId in distinct)
        {
            var tier = await _packages.GetProjectionByIdAsync(packageId, cancellationToken);
            var rules = await _rules.GetLiveRulesAsync(packageId, cancellationToken);

            var rulePools = rules
                .Select(rule => pools.TryGetValue(rule.CourseId, out var dishes)
                    ? dishes.Select(dish => dish.FoodCost)
                    : Enumerable.Empty<decimal?>())
                .ToList();

            var standardRun = PackageCostingCalculator.StandardRunFoodCostPerPax(rulePools);

            estimates[packageId] = new BookingTierEstimate(
                PackageId: packageId,
                AmountPax: tier?.AmountPax,
                MinPax: tier?.MinPax,
                StandardRunFoodCostPerPax: standardRun,
                BaseGrossProfitPercentage: PackageCostingCalculator.BaseGrossProfitPercentage(
                    tier?.AmountPax, standardRun),
                CourseRuleCount: rules.Count);
        }

        return estimates;
    }
}
