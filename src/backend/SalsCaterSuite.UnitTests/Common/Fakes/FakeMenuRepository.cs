namespace SalsCaterSuite.UnitTests.Common.Fakes;

using SalsCaterSuite.Application.Common.Interfaces;
using SalsCaterSuite.Application.Common.Models;
using SalsCaterSuite.Application.Features.Menus.DTOs;
using SalsCaterSuite.Application.Features.Menus.Models;
using SalsCaterSuite.Domain.Entities;

/// <summary>
/// Hand-written fake of <see cref="IMenuRepository"/> (DEC-037). No EF Core is involved;
/// in-place updates are visible through the shared reference-tracking store.
/// </summary>
public sealed class FakeMenuRepository : IMenuRepository
{
    private readonly FakeSalsCaterSuiteDataStore _store;

    public FakeMenuRepository(FakeSalsCaterSuiteDataStore store)
    {
        _store = store;
    }

    /// <summary>Number of successful saves, for asserting the commit boundary.</summary>
    public int SaveCount { get; private set; }

    /// <summary>
    /// A single dish by its code. Soft-deleted dishes are excluded (DEC-063).
    /// </summary>
    public Task<Menu?> GetByIdAsync(string menuId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_store.Menus.FirstOrDefault(m => m.MenuId == menuId && !m.IsDeleted));
    }

    /// <summary>
    /// True when a dish with this code already exists — soft-deleted dishes included, because
    /// the code is the primary key (DEC-063).
    /// </summary>
    public Task<bool> ExistsAsync(string menuId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_store.Menus.Any(m => m.MenuId == menuId));
    }

    /// <summary>All live menus ordered by name — the menu-costing list projection source.</summary>
    public Task<IReadOnlyList<Menu>> GetOrderedByNameAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<Menu> result = _store.Menus.Where(m => !m.IsDeleted).OrderBy(m => m.MenuName).ToList();
        return Task.FromResult(result);
    }

    public Task<List<MenuDto>> GetProjectionListAsync(int? courseId, CancellationToken cancellationToken = default)
    {
        IEnumerable<Menu> menus = _store.Menus.Where(m => !m.IsDeleted);

        if (courseId.HasValue)
        {
            menus = menus.Where(m => m.CourseId == courseId.Value);
        }

        var result = menus
            .OrderBy(m => m.CourseId).ThenBy(m => m.MenuName)
            .Select(Map)
            .ToList();

        return Task.FromResult(result);
    }

    public Task<MenuDto?> GetProjectionByIdAsync(string menuId, CancellationToken cancellationToken = default)
    {
        var menu = _store.Menus.FirstOrDefault(m => m.MenuId == menuId && !m.IsDeleted);
        return Task.FromResult(menu is null ? null : Map(menu));
    }

    public Task<List<CourseDto>> GetCourseProjectionsAsync(CancellationToken cancellationToken = default)
    {
        var result = _store.CourseCategories
            .OrderBy(c => c.CourseId)
            .Select(c => new CourseDto { CourseId = c.CourseId, CourseName = c.CourseName })
            .ToList();

        return Task.FromResult(result);
    }

    /// <summary>
    /// The menu-catalogue costing aggregate (spec §7 R4) — the in-memory mirror of the EF
    /// implementation, including the soft-delete exclusion.
    /// </summary>
    public Task<MenuCostingAggregate> GetDashboardCostingAggregateAsync(CancellationToken cancellationToken = default)
    {
        var menus = _store.Menus.Where(m => !m.IsDeleted).ToList();
        var marginPopulation = menus
            .Where(m => m.FoodCost > 0m && m.Price > 0m)
            .ToList();

        return Task.FromResult(new MenuCostingAggregate
        {
            TotalCount = menus.Count,
            CostedCount = menus.Count(m => m.FoodCost > 0m),
            CatalogueRevenueAmount = marginPopulation.Sum(m => m.Price ?? 0m),
            CatalogueFoodCostAmount = marginPopulation.Sum(m => m.FoodCost ?? 0m)
        });
    }

    /// <summary>
    /// The per-course food-cost breakdown (spec §7 R4) — the in-memory mirror of the EF
    /// implementation's <c>GROUP BY courseId</c>.
    /// </summary>
    public Task<IReadOnlyList<MenuCourseCostAggregate>> GetDashboardCourseCostAggregatesAsync(CancellationToken cancellationToken = default)
    {
        var result = _store.Menus
            .Where(m => !m.IsDeleted && m.FoodCost > 0m)
            .GroupBy(m => m.CourseId)
            .Select(g => new MenuCourseCostAggregate
            {
                CourseId = g.Key,
                FoodCostAmount = g.Sum(m => m.FoodCost ?? 0m),
                MenuCount = g.Count()
            })
            .ToList();

        return Task.FromResult<IReadOnlyList<MenuCourseCostAggregate>>(result);
    }

    public Task<bool> CourseExistsAsync(int courseId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_store.CourseCategories.Any(c => c.CourseId == courseId));
    }

    /// <summary>
    /// The Menu &amp; Food Costing Master read — the in-memory mirror of the EF implementation:
    /// the same filters, the same ordering, the same aggregation and the same soft-delete
    /// exclusion (spec §6.1).
    /// </summary>
    public Task<MenuMasterDataSet> GetMenuMasterDataSetAsync(
        MenuMasterFilter filter,
        CancellationToken cancellationToken = default)
    {
        var filtered = ApplyFilters(filter).ToList();
        var marginPopulation = filtered
            .Where(m => (m.Price ?? 0m) > 0m && (m.FoodCost ?? 0m) > 0m)
            .ToList();

        var courseMargins = marginPopulation
            .GroupBy(m => m.CourseId)
            .Select(g => new MenuCourseMarginAggregate
            {
                CourseId = g.Key,
                RevenueAmount = g.Sum(m => m.Price ?? 0m),
                FoodCostAmount = g.Sum(m => m.FoodCost ?? 0m),
                DishCount = g.Count()
            })
            .ToList();

        var rows = ApplySort(filtered, filter.Sort)
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .Select(MapRow)
            .ToList();

        return Task.FromResult(new MenuMasterDataSet
        {
            Rows = rows,
            Aggregate = new MenuMasterAggregate
            {
                TotalCount = filtered.Count,
                CostedCount = filtered.Count(m => (m.FoodCost ?? 0m) > 0m),
                MarginPopulationCount = marginPopulation.Count,
                RevenueAmount = marginPopulation.Sum(m => m.Price ?? 0m),
                FoodCostAmount = marginPopulation.Sum(m => m.FoodCost ?? 0m)
            },
            CourseMargins = courseMargins,
            Courses = GetCourseScope(filter),
            MarginAlertCount = filtered.Count(m => FoodCostPercentage(m) > filter.TargetFoodCostPercentage)
        });
    }

    /// <summary>The whole filtered result set, never paged (the export's source).</summary>
    public Task<List<MenuMasterRow>> GetMenuMasterRowsAsync(
        MenuMasterFilter filter,
        CancellationToken cancellationToken = default)
    {
        var rows = ApplySort(ApplyFilters(filter).ToList(), filter.Sort)
            .Select(MapRow)
            .ToList();

        return Task.FromResult(rows);
    }

    public Task AddAsync(Menu menu, CancellationToken cancellationToken = default)
    {
        _store.Menus.Add(menu);
        return Task.CompletedTask;
    }

    public void Remove(Menu menu)
    {
        _store.Menus.Remove(menu);
    }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        SaveCount++;
        return Task.FromResult(1);
    }

    private MenuDto Map(Menu menu) => new()
    {
        MenuId = menu.MenuId,
        CourseId = menu.CourseId,
        CourseName = _store.CourseCategories
            .FirstOrDefault(c => c.CourseId == menu.CourseId)?.CourseName,
        MenuName = menu.MenuName ?? string.Empty,
        Note = menu.Note,
        Price = menu.Price,
        FoodCost = menu.FoodCost,
        Image = menu.Image
    };

    private MenuMasterRow MapRow(Menu menu) => new()
    {
        MenuId = menu.MenuId,
        MenuName = menu.MenuName ?? string.Empty,
        CourseId = menu.CourseId,
        CourseName = _store.CourseCategories
            .FirstOrDefault(c => c.CourseId == menu.CourseId)?.CourseName,
        Note = menu.Note,
        Image = menu.Image,
        Price = menu.Price,
        FoodCost = menu.FoodCost
    };

    /// <summary>
    /// The filters of the master read — the in-memory mirror of the EF implementation. The
    /// soft-deleted dishes are excluded, exactly as the SQL does (DEC-045, DEC-050, DEC-063).
    /// </summary>
    private List<Menu> ApplyFilters(MenuMasterFilter filter)
    {
        var menus = ApplyScopeFilters(filter);

        if (filter.CourseId.HasValue)
        {
            menus = menus.Where(m => m.CourseId == filter.CourseId.Value);
        }

        return menus.ToList();
    }

    /// <summary>
    /// The tab-strip scope — the search and the status filter, but not the course filter.
    /// </summary>
    private IEnumerable<Menu> ApplyScopeFilters(MenuMasterFilter filter)
    {
        IEnumerable<Menu> menus = _store.Menus.Where(m => !m.IsDeleted);

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var term = filter.Search.ToLower();
            menus = menus.Where(m =>
                m.MenuId.ToLower().Contains(term) ||
                (m.MenuName ?? string.Empty).ToLower().Contains(term) ||
                (m.Note != null && m.Note.ToLower().Contains(term)));
        }

        switch (filter.Status)
        {
            case MenuMasterStatus.Costed:
                menus = menus.Where(m => (m.FoodCost ?? 0m) > 0m);
                break;

            case MenuMasterStatus.Uncosted:
                menus = menus.Where(m => (m.FoodCost ?? 0m) <= 0m);
                break;

            case MenuMasterStatus.MarginAlert:
                menus = menus.Where(m => FoodCostPercentage(m) > filter.TargetFoodCostPercentage);
                break;
        }

        return menus.ToList();
    }

    /// <summary>The ordering of the master read — the in-memory mirror of the EF implementation.</summary>
    private static IEnumerable<Menu> ApplySort(IEnumerable<Menu> menus, MenuMasterSort sort)
    {
        var ordered = sort switch
        {
            MenuMasterSort.FoodCostDesc => menus.OrderByDescending(FoodCostPercentage),
            MenuMasterSort.PriceDesc => menus.OrderByDescending(m => m.Price),
            MenuMasterSort.PriceAsc => menus.OrderBy(m => m.Price),
            MenuMasterSort.MarginDesc => menus.OrderByDescending(GrossMarginPercentage),
            MenuMasterSort.NameAsc => menus.OrderBy(m => m.MenuName),
            _ => menus.OrderBy(FoodCostPercentage)
        };

        // Every sort carries a stable tiebreak on the dish code.
        return ordered.ThenBy(m => m.MenuId);
    }

    /// <summary>
    /// The <b>rounded</b> food-cost percentage — the expression the SQL margin-alert filter and
    /// the KPI-24 count use (<c>ROUND(…, 2)</c> in SQL Server, which rounds away from zero).
    /// The in-memory mirror must use the explicit <see cref="MidpointRounding.AwayFromZero"/>,
    /// because the two-argument <c>Math.Round</c> in C# would be half-to-even.
    /// </summary>
    private static decimal FoodCostPercentage(Menu menu)
    {
        var foodCost = menu.FoodCost ?? 0m;
        var price = menu.Price ?? 0m;
        var divisor = price > 0m ? price : 1m;
        return Math.Round(foodCost / divisor * 100m, 2, MidpointRounding.AwayFromZero);
    }

    /// <summary>The unrounded gross-margin percentage — the expression the margin sort uses.</summary>
    private static decimal GrossMarginPercentage(Menu menu)
    {
        var foodCost = menu.FoodCost ?? 0m;
        var price = menu.Price ?? 0m;
        if (price <= 0m)
        {
            return 0m;
        }

        return (price - foodCost) / price * 100m;
    }

    /// <summary>
    /// The course tab strip: the scope's total and one entry per course category with its live
    /// dish count. The scope is everything except the course filter.
    /// </summary>
    private MenuCoursesDto GetCourseScope(MenuMasterFilter filter)
    {
        var scope = ApplyScopeFilters(filter);

        var counts = scope
            .Where(m => m.CourseId.HasValue)
            .GroupBy(m => m.CourseId!.Value)
            .ToDictionary(g => g.Key, g => g.Count());

        return new MenuCoursesDto
        {
            Total = scope.Count(),
            Items = _store.CourseCategories
                .OrderBy(c => c.CourseId)
                .Select(c => new MenuCourseCountDto
                {
                    CourseId = c.CourseId,
                    CourseName = c.CourseName,
                    DishCount = counts.TryGetValue(c.CourseId, out var dishCount) ? dishCount : 0
                })
                .ToList()
        };
    }
}
