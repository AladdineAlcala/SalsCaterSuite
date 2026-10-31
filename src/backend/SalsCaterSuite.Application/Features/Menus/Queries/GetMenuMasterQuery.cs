using SalsCaterSuite.Application.Common.Interfaces;
using SalsCaterSuite.Application.Common.Models;
using SalsCaterSuite.Application.Features.Menus.DTOs.Request;
using SalsCaterSuite.Application.Features.Menus.DTOs.Response;
using SalsCaterSuite.Application.Features.Menus.Models;
using SalsCaterSuite.Application.Features.Menus.Services;

namespace SalsCaterSuite.Application.Features.Menus.Queries;

/// <summary>
/// GET /api/menus/master — the paged Menu &amp; Food Costing Master (spec §6.1, REQ-MNU-01/02).
/// <para>
/// The use case owns the contract: <c>page</c> is clamped to 1 and <c>pageSize</c> to 1…100
/// (default 12). An unrecognised <c>status</c> is <b>ignored</b> and an unrecognised
/// <c>sort</c> falls back to <c>FoodCostAsc</c> — neither is rejected with a 400 (spec §6.1).
/// </para>
/// <para>
/// Every published figure is computed here, server-side (DEC-002), through the single
/// <see cref="MenuCostingCalculator"/> the menu-costing service also uses, so a dish's card, its
/// margin preview and its costing response can never disagree.
/// </para>
/// </summary>
public class GetMenuMasterQuery
{
    /// <summary>The default page size of the master (spec §4.7).</summary>
    public const int DefaultPageSize = 12;

    /// <summary>The largest page the master will serve.</summary>
    public const int MaxPageSize = 100;

    /// <summary>The label used for the dishes that are not assigned to a course (KPI-16, KPI-23).</summary>
    private const string UncategorisedCourseName = "Uncategorised";

    private readonly IMenuRepository _menus;
    private readonly IFoodCostBenchmarkProvider _benchmarks;
    private readonly IDateTime _clock;

    public GetMenuMasterQuery(
        IMenuRepository menus,
        IFoodCostBenchmarkProvider benchmarks,
        IDateTime clock)
    {
        _menus = menus;
        _benchmarks = benchmarks;
        _clock = clock;
    }

    /// <summary>
    /// The paged master. The <see cref="MenuMasterResponse.Summary"/> describes the whole filtered
    /// result set, not the returned page, while the <see cref="MenuMasterResponse.Courses"/> describe
    /// the whole catalogue so the tab counts stay stable while a scope is selected.
    /// </summary>
    public async Task<Result<MenuMasterResponse>> ExecuteAsync(
        string? search,
        int? courseId,
        string? status,
        string? sort,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var filter = BuildFilter(search, courseId, status, sort, page, pageSize);
        var data = await _menus.GetMenuMasterDataSetAsync(filter, cancellationToken);

        var totalPages = (int)Math.Ceiling(data.Aggregate.TotalCount / (double)filter.PageSize);

        // The benchmark is read once, so the summary and the rows always carry the same target.
        var benchmark = _benchmarks.Current;
        var target = benchmark.TargetFoodCostPercentage;

        return Result<MenuMasterResponse>.Success(new MenuMasterResponse
        {
            Summary = BuildSummary(data, benchmark),
            Courses = data.Courses,
            Items = data.Rows.Select(row => ComposeItem(row, target)).ToList(),
            Page = filter.Page,
            PageSize = filter.PageSize,
            TotalCount = data.Aggregate.TotalCount,
            TotalPages = totalPages,
            GeneratedAt = _clock.UtcNow
        });
    }

    /// <summary>
    /// The whole filtered result set, without paging. The XLSX export is the current filtered
    /// result set, not the current page (spec §6.3).
    /// </summary>
    public async Task<Result<List<MenuMasterItemResponse>>> ExecuteUnpagedAsync(
        string? search,
        int? courseId,
        string? status,
        string? sort,
        CancellationToken cancellationToken = default)
    {
        var filter = BuildFilter(search, courseId, status, sort, page: 1, pageSize: MaxPageSize);
        var rows = await _menus.GetMenuMasterRowsAsync(filter, cancellationToken);

        var target = _benchmarks.Current.TargetFoodCostPercentage;

        return Result<List<MenuMasterItemResponse>>.Success(
            rows.Select(row => ComposeItem(row, target)).ToList());
    }

    /// <summary>
    /// The normalised filter. The clamping and the fallbacks are the use case's policy; the
    /// repository only translates the result into SQL.
    /// </summary>
    private MenuMasterFilter BuildFilter(
        string? search,
        int? courseId,
        string? status,
        string? sort,
        int page,
        int pageSize)
    {
        return new MenuMasterFilter
        {
            Search = string.IsNullOrWhiteSpace(search) ? null : search,
            CourseId = courseId,
            Status = ParseStatus(status),
            Sort = ParseSort(sort),
            TargetFoodCostPercentage = _benchmarks.Current.TargetFoodCostPercentage,
            Page = ClampPage(page),
            PageSize = ClampPageSize(pageSize)
        };
    }

    /// <summary>
    /// The KPI cards of the master (spec §4.2). Every figure is measured over the whole filtered
    /// result set.
    /// </summary>
    private MenuMasterSummaryResponse BuildSummary(MenuMasterDataSet data, FoodCostBenchmark benchmark)
    {
        var aggregate = data.Aggregate;

        // KPI-14 — the costing coverage. Zero when the filtered set is empty: a population ratio
        // with a defined zero (the dashboard's own zero-denominator rule).
        var coveragePercentage = aggregate.TotalCount > 0
            ? Round((decimal)aggregate.CostedCount / aggregate.TotalCount * 100m)
            : 0m;

        // KPI-15's catalogue margin, over exactly the margin population the dashboard uses, and
        // KPI-22's average food cost as its complement. Publishing the complement (instead of a
        // second, separately rounded division) is what makes "100 − averageFoodCostPercentage"
        // equal the dashboard's catalogue margin to the penny (AC-M4, AC-M13).
        decimal? catalogueMarginPercentage = aggregate.RevenueAmount > 0m
            ? Round((aggregate.RevenueAmount - aggregate.FoodCostAmount) / aggregate.RevenueAmount * 100m)
            : null;

        decimal? averageFoodCostPercentage = catalogueMarginPercentage.HasValue
            ? 100m - catalogueMarginPercentage.Value
            : null;

        return new MenuMasterSummaryResponse
        {
            TotalDishes = aggregate.TotalCount,
            CostedDishes = aggregate.CostedCount,
            UncostedDishes = aggregate.TotalCount - aggregate.CostedCount,
            CostingCoveragePercentage = coveragePercentage,
            AverageFoodCostPercentage = averageFoodCostPercentage,
            AverageFoodCostHeadroomPercentage = averageFoodCostPercentage.HasValue
                ? benchmark.TargetFoodCostPercentage - averageFoodCostPercentage.Value
                : null,
            TargetFoodCostPercentage = benchmark.TargetFoodCostPercentage,
            TargetMarginPercentage = benchmark.TargetMarginPercentage,
            IsBenchmarkConfigured = benchmark.IsConfigured,
            AverageSellingPrice = aggregate.MarginPopulationCount > 0
                ? Round(aggregate.RevenueAmount / aggregate.MarginPopulationCount)
                : 0m,
            MarginAlertCount = data.MarginAlertCount,
            BestMarginCourse = BuildBestMarginCourse(data)
        };
    }

    /// <summary>
    /// KPI-23 — the course with the highest <c>Σ(Price − FoodCost) ÷ Σ Price × 100</c> over its
    /// margin population. <c>null</c> when the margin population is empty. A dish without a
    /// course is grouped under <c>"Uncategorised"</c>, exactly as KPI-16 does, and may be the
    /// reported course.
    /// </summary>
    private MenuBestMarginCourseResponse? BuildBestMarginCourse(MenuMasterDataSet data)
    {
        var courseNames = data.Courses.Items.ToDictionary(c => c.CourseId, c => c.CourseName);

        var best = data.CourseMargins
            .Where(c => c.RevenueAmount > 0m && c.DishCount > 0)
            .Select(c => new
            {
                c.CourseId,
                c.RevenueAmount,
                c.FoodCostAmount,
                c.DishCount,
                Margin = Round((c.RevenueAmount - c.FoodCostAmount) / c.RevenueAmount * 100m)
            })
            .OrderByDescending(c => c.Margin)
            .ThenBy(c => ResolveCourseName(c.CourseId, courseNames), StringComparer.Ordinal)
            .ThenBy(c => c.CourseId)
            .FirstOrDefault();

        if (best is null)
        {
            return null;
        }

        return new MenuBestMarginCourseResponse
        {
            CourseId = best.CourseId,
            CourseName = ResolveCourseName(best.CourseId, courseNames),
            GrossMarginPercentage = best.Margin,
            AverageSellingPrice = Round(best.RevenueAmount / best.DishCount),
            DishCount = best.DishCount
        };
    }

    /// <summary>One dish card — every figure derived through the shared calculator.</summary>
    private static MenuMasterItemResponse ComposeItem(MenuMasterRow row, decimal targetFoodCostPercentage)
    {
        var sellingPrice = row.Price ?? 0m;
        var foodCost = row.FoodCost ?? 0m;
        var foodCostPercentage = MenuCostingCalculator.FoodCostPercentage(sellingPrice, foodCost);

        return new MenuMasterItemResponse
        {
            MenuId = row.MenuId,
            MenuName = row.MenuName,
            CourseId = row.CourseId,
            CourseName = row.CourseName,
            Note = row.Note,
            Image = row.Image,
            SellingPrice = sellingPrice,
            FoodCost = foodCost,
            GrossProfit = MenuCostingCalculator.GrossProfit(sellingPrice, foodCost),
            FoodCostPercentage = foodCostPercentage,
            GrossMarginPercentage = MenuCostingCalculator.GrossMarginPercentage(sellingPrice, foodCost),
            TargetFoodCostPercentage = targetFoodCostPercentage,
            IsWithinTarget = foodCostPercentage <= targetFoodCostPercentage,
            MarginVariancePercentage = foodCostPercentage - targetFoodCostPercentage
        };
    }

    private static string ResolveCourseName(
        int? courseId,
        IReadOnlyDictionary<int, string> courseNames) =>
        courseId.HasValue && courseNames.TryGetValue(courseId.Value, out var name) && !string.IsNullOrWhiteSpace(name)
            ? name
            : UncategorisedCourseName;

    /// <summary>
    /// The status filter. An unrecognised value is ignored, never rejected (spec §6.1).
    /// </summary>
    private static MenuMasterStatus ParseStatus(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return MenuMasterStatus.All;
        }

        var value = status.Trim();

        if (value.Equals(nameof(MenuMasterStatus.Costed), StringComparison.OrdinalIgnoreCase))
        {
            return MenuMasterStatus.Costed;
        }

        if (value.Equals(nameof(MenuMasterStatus.Uncosted), StringComparison.OrdinalIgnoreCase))
        {
            return MenuMasterStatus.Uncosted;
        }

        if (value.Equals(nameof(MenuMasterStatus.MarginAlert), StringComparison.OrdinalIgnoreCase))
        {
            return MenuMasterStatus.MarginAlert;
        }

        return MenuMasterStatus.All;
    }

    /// <summary>
    /// The sort key. An unrecognised value falls back to <c>FoodCostAsc</c>, never a 400
    /// (spec §6.1).
    /// </summary>
    private static MenuMasterSort ParseSort(string? sort)
    {
        if (string.IsNullOrWhiteSpace(sort))
        {
            return MenuMasterSort.FoodCostAsc;
        }

        var value = sort.Trim();

        if (value.Equals(nameof(MenuMasterSort.FoodCostAsc), StringComparison.OrdinalIgnoreCase))
        {
            return MenuMasterSort.FoodCostAsc;
        }

        if (value.Equals(nameof(MenuMasterSort.FoodCostDesc), StringComparison.OrdinalIgnoreCase))
        {
            return MenuMasterSort.FoodCostDesc;
        }

        if (value.Equals(nameof(MenuMasterSort.PriceDesc), StringComparison.OrdinalIgnoreCase))
        {
            return MenuMasterSort.PriceDesc;
        }

        if (value.Equals(nameof(MenuMasterSort.PriceAsc), StringComparison.OrdinalIgnoreCase))
        {
            return MenuMasterSort.PriceAsc;
        }

        if (value.Equals(nameof(MenuMasterSort.MarginDesc), StringComparison.OrdinalIgnoreCase))
        {
            return MenuMasterSort.MarginDesc;
        }

        if (value.Equals(nameof(MenuMasterSort.NameAsc), StringComparison.OrdinalIgnoreCase))
        {
            return MenuMasterSort.NameAsc;
        }

        return MenuMasterSort.FoodCostAsc;
    }

    private static int ClampPage(int page) => page < 1 ? 1 : page;

    private static int ClampPageSize(int pageSize) => pageSize switch
    {
        < 1 => 1,
        > MaxPageSize => MaxPageSize,
        _ => pageSize
    };

    /// <summary>
    /// Rounds to the two decimal places the reporting standard requires (DEC-003), away from the
    /// midpoint — the same convention the operations dashboard uses.
    /// </summary>
    private static decimal Round(decimal value) =>
        Math.Round(value, 2, MidpointRounding.AwayFromZero);
}
