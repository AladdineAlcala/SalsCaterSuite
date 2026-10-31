using SalsCaterSuite.Application.Common.Interfaces;
using SalsCaterSuite.Application.Common.Models;
using SalsCaterSuite.Application.Features.Packages.DTOs.Request;
using SalsCaterSuite.Application.Features.Packages.DTOs.Response;
using SalsCaterSuite.Application.Features.Packages.Models;
using SalsCaterSuite.Application.Features.Packages.Services;
using SalsCaterSuite.Domain.Entities;

namespace SalsCaterSuite.Application.Features.Packages.Queries;

/// <summary>
/// GET /api/packages/{id}/course-rules — the tier's course-coverage ruleset, its qualified pools
/// and its planning figures (contract §13.1, REQ-PKG-02).
/// <para>
/// The tier is resolved through the <b>catalogue population</b> (<c>IsDeleted == false</c>,
/// DEC-074): a soft-deleted tier is not part of the catalogue and answers <c>404</c>. This is
/// deliberately stricter than <c>IPackageRepository.GetByIdAsync</c>, which stays unfiltered for
/// the booking path (BR-04).
/// </para>
/// <para>
/// Every figure is computed on the server through <see cref="PackageCostingCalculator"/>, and the
/// food-cost benchmark is read <b>once</b> per request, so the ruleset and any preview built on
/// it can never disagree (DEC-002, DEC-040).
/// </para>
/// </summary>
public class GetPackageCourseRulesetQuery
{
    private readonly IPackageRepository _packages;
    private readonly IPackageCourseRuleRepository _rules;
    private readonly IMenuRepository _menus;
    private readonly IFoodCostBenchmarkProvider _benchmarks;
    private readonly IDateTime _clock;

    public GetPackageCourseRulesetQuery(
        IPackageRepository packages,
        IPackageCourseRuleRepository rules,
        IMenuRepository menus,
        IFoodCostBenchmarkProvider benchmarks,
        IDateTime clock)
    {
        _packages = packages;
        _rules = rules;
        _menus = menus;
        _benchmarks = benchmarks;
        _clock = clock;
    }

    public async Task<Result<PackageCourseRulesetResponse>> ExecuteAsync(
        int packageId,
        CancellationToken cancellationToken = default)
    {
        var package = await _packages.GetProjectionByIdAsync(packageId, cancellationToken);

        if (package is null)
        {
            return Result<PackageCourseRulesetResponse>.Failure("Package not found.");
        }

        // The benchmark is read once for the whole request (DEC-040).
        var benchmark = _benchmarks.Current;

        var projections = await _rules.GetLiveRulesAsync(packageId, cancellationToken);

        // The qualified pool of a rule is derived, never stored: it is the live dishes of the
        // rule's course (DEC-083, domain model §2.4.1).
        var menus = await _menus.GetOrderedByNameAsync(cancellationToken);
        var pools = menus
            .Where(menu => menu.CourseId.HasValue)
            .GroupBy(menu => menu.CourseId!.Value)
            .ToDictionary(group => group.Key, group => group.ToList());

        var rules = projections
            .Select(projection => MapRule(projection, pools))
            .OrderBy(rule => rule.CourseName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(rule => rule.CourseId)
            .ToList();

        // The standard run sums the pool means at full precision and rounds once (DEC-083).
        var standardRun = PackageCostingCalculator.StandardRunFoodCostPerPax(
            rules.Select(rule => rule.Pool.Select(dish => dish.FoodCost)));

        var ruleset = new PackageCourseRulesetResponse
        {
            PackageId = package.PackageId,
            PackageName = package.Description,
            PackageCode = package.PackageCode,
            PackageType = package.PackageType,
            ExtendedLocationAmount = package.ExtendedLocationAmount,
            AmountPax = package.AmountPax,
            MinPax = package.MinPax,
            IsActive = package.IsActive,
            TargetFoodCostPercentage = benchmark.TargetFoodCostPercentage,
            TargetMarginPercentage = benchmark.TargetMarginPercentage,
            IsBenchmarkConfigured = benchmark.IsConfigured,
            CostCeilingPerPax = PackageCostingCalculator.CostCeilingPerPax(
                package.AmountPax, benchmark.TargetFoodCostPercentage),
            MinimumContractValue = PackageCostingCalculator.MinimumContractValue(
                package.AmountPax, package.MinPax),
            StandardRunFoodCostPerPax = standardRun,
            StandardRunFoodCostPercentage = PackageCostingCalculator.StandardRunFoodCostPercentage(
                standardRun, package.AmountPax),
            BaseGrossProfitPercentage = PackageCostingCalculator.BaseGrossProfitPercentage(
                package.AmountPax, standardRun),
            Rules = rules,
            GeneratedAt = _clock.UtcNow
        };

        return Result<PackageCourseRulesetResponse>.Success(ruleset);
    }

    /// <summary>Maps one rule and derives its qualified pool.</summary>
    private static PackageCourseRuleResponse MapRule(
        PackageCourseRuleProjection projection,
        IReadOnlyDictionary<int, List<Menu>> pools)
    {
        var pool = pools.TryGetValue(projection.CourseId, out var dishes)
            ? dishes
            : new List<Menu>();

        var poolDtos = pool
            .OrderBy(menu => menu.MenuName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(menu => menu.MenuId, StringComparer.OrdinalIgnoreCase)
            .Select(menu => new PackageCoursePoolDishResponse
            {
                MenuId = menu.MenuId,
                MenuName = menu.MenuName,
                FoodCost = menu.FoodCost,
                Price = menu.Price
            })
            .ToList();

        return new PackageCourseRuleResponse
        {
            PackageCourseRuleId = projection.PackageCourseRuleId,
            CourseId = projection.CourseId,
            CourseName = projection.CourseName,
            RequiredQuota = projection.RequiredQuota,
            PortionGrams = projection.PortionGrams,
            RuleDescription = projection.RuleDescription,
            PoolSize = poolDtos.Count,
            // A dish is costed when its food cost is greater than zero (DEC-087).
            PoolCostedCount = poolDtos.Count(dish => dish.FoodCost.HasValue && dish.FoodCost.Value > 0m),
            PoolAverageFoodCost = PackageCostingCalculator.PoolAverageFoodCost(
                poolDtos.Select(dish => dish.FoodCost)),
            Pool = poolDtos
        };
    }
}
