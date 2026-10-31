namespace SalsCaterSuite.Infrastructure.Persistence.Repositories;

using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using SalsCaterSuite.Application.Common.Interfaces;
using SalsCaterSuite.Application.Common.Models;
using SalsCaterSuite.Application.Features.Menus.DTOs.Request;
using SalsCaterSuite.Application.Features.Menus.DTOs.Response;
using SalsCaterSuite.Application.Features.Menus.Models;
using SalsCaterSuite.Domain.Entities;

/// <summary>
/// EF Core implementation of the menu repository (DEC-037). Depends on the concrete
/// <see cref="SalsCaterSuiteDbContext"/>; all EF Core knowledge stays inside the
/// Infrastructure layer. The projections and query shapes are byte-for-byte the ones
/// previously inlined in the Menus feature use cases.
/// </summary>
public class MenuRepository : IMenuRepository
{
    private readonly SalsCaterSuiteDbContext _context;

    public MenuRepository(SalsCaterSuiteDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// A single dish by its code.
    /// <para>
    /// Soft-deleted dishes are excluded (DEC-063): the master query, the legacy list, the
    /// costing endpoints and the write commands all agree that a deleted dish is gone. The
    /// exclusion is scoped to the menu feature's own reads — no global query filter is
    /// introduced (DEC-038, DEC-045, DEC-050).
    /// </para>
    /// </summary>
    public async Task<Menu?> GetByIdAsync(string menuId, CancellationToken cancellationToken = default)
    {
        return await _context.Menus
            .FirstOrDefaultAsync(m => m.MenuId == menuId && !m.IsDeleted, cancellationToken);
    }

    /// <summary>
    /// True when a dish with this code already exists.
    /// <para>
    /// The check deliberately covers <b>soft-deleted</b> dishes too: the code is the primary
    /// key, so a deleted dish still occupies it and a new dish with the same code would be a
    /// key collision (a 500) rather than a conflict (a 409).
    /// </para>
    /// </summary>
    public async Task<bool> ExistsAsync(string menuId, CancellationToken cancellationToken = default)
    {
        return await _context.Menus
            .AnyAsync(m => m.MenuId == menuId, cancellationToken);
    }

    /// <summary>All live menus ordered by name — the menu-costing list projection source.</summary>
    public async Task<IReadOnlyList<Menu>> GetOrderedByNameAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Menus
            .Where(m => !m.IsDeleted)
            .OrderBy(m => m.MenuName)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// The menu list projection (course name included), optionally filtered by course.
    /// Soft-deleted dishes are excluded (DEC-063).
    /// </summary>
    public async Task<List<MenuResponse>> GetProjectionListAsync(int? courseId, CancellationToken cancellationToken = default)
    {
        IQueryable<Menu> query = _context.Menus.AsNoTracking()
            .Where(m => !m.IsDeleted)
            .Include(m => m.CourseCategory);

        if (courseId.HasValue)
            query = query.Where(m => m.CourseId == courseId.Value);

        return await query
            .OrderBy(m => m.CourseId).ThenBy(m => m.MenuName)
            .Select(m => new MenuResponse
            {
                MenuId = m.MenuId,
                CourseId = m.CourseId,
                CourseName = m.CourseCategory != null ? m.CourseCategory.CourseName : null,
                MenuName = m.MenuName ?? string.Empty,
                Note = m.Note,
                Price = m.Price,
                FoodCost = m.FoodCost,
                Image = m.Image
            })
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// The single-menu projection (course name included). A soft-deleted dish is not found
    /// (DEC-063).
    /// </summary>
    public async Task<MenuResponse?> GetProjectionByIdAsync(string menuId, CancellationToken cancellationToken = default)
    {
        return await _context.Menus
            .AsNoTracking()
            .Where(m => !m.IsDeleted)
            .Include(m => m.CourseCategory)
            .Where(m => m.MenuId == menuId)
            .Select(m => new MenuResponse
            {
                MenuId = m.MenuId,
                CourseId = m.CourseId,
                CourseName = m.CourseCategory != null ? m.CourseCategory.CourseName : null,
                MenuName = m.MenuName ?? string.Empty,
                Note = m.Note,
                Price = m.Price,
                FoodCost = m.FoodCost,
                Image = m.Image
            })
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<List<CourseResponse>> GetCourseProjectionsAsync(CancellationToken cancellationToken = default)
    {
        return await _context.CourseCategories
            .AsNoTracking()
            .OrderBy(c => c.CourseId)
            .Select(c => new CourseResponse { CourseId = c.CourseId, CourseName = c.CourseName })
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(Menu menu, CancellationToken cancellationToken = default)
    {
        await _context.Menus.AddAsync(menu, cancellationToken);
    }

    /// <summary>
    /// The menu-catalogue costing aggregate (spec §7 R4, KPI-14, KPI-15). Soft-deleted menus
    /// are excluded; the exclusion is scoped to this dashboard query and no global query filter
    /// is introduced (DEC-038, spec §6.13).
    /// </summary>
    public async Task<MenuCostingAggregate> GetDashboardCostingAggregateAsync(CancellationToken cancellationToken = default)
    {
        var query = _context.Menus
            .AsNoTracking()
            .Where(m => !m.IsDeleted);

        var totalCount = await query.CountAsync(cancellationToken);
        var costedCount = await query.CountAsync(m => m.FoodCost > 0m, cancellationToken);

        // The margin population: costed menus that also carry a selling price.
        var marginPopulation = query.Where(m => m.FoodCost > 0m && m.Price > 0m);

        var catalogueRevenue = await marginPopulation.SumAsync(m => (decimal?)m.Price, cancellationToken) ?? 0m;
        var catalogueFoodCost = await marginPopulation.SumAsync(m => (decimal?)m.FoodCost, cancellationToken) ?? 0m;

        return new MenuCostingAggregate
        {
            TotalCount = totalCount,
            CostedCount = costedCount,
            CatalogueRevenueAmount = catalogueRevenue,
            CatalogueFoodCostAmount = catalogueFoodCost
        };
    }

    /// <summary>
    /// The per-course food-cost breakdown (spec §7 R4, KPI-16). The grouping is done in SQL;
    /// the course names are resolved by the Application layer from the course-category
    /// projection, which keeps the query a simple, translatable <c>GROUP BY</c>.
    /// </summary>
    public async Task<IReadOnlyList<MenuCourseCostAggregate>> GetDashboardCourseCostAggregatesAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Menus
            .AsNoTracking()
            .Where(m => !m.IsDeleted && m.FoodCost > 0m)
            .GroupBy(m => m.CourseId)
            .Select(g => new MenuCourseCostAggregate
            {
                CourseId = g.Key,
                FoodCostAmount = g.Sum(m => m.FoodCost) ?? 0m,
                MenuCount = g.Count()
            })
            .ToListAsync(cancellationToken);
    }

    public void Remove(Menu menu)
    {
        _context.Menus.Remove(menu);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // The Menu & Food Costing Master (spec §6.1 / §6.3, DEC-045 / DEC-050)
    // ═══════════════════════════════════════════════════════════════════════════
    /// <summary>
    /// The <b>unrounded</b> food-cost ratio of a dish, as a translatable SQL expression.
    /// <para>
    /// The price guard keeps the SQL division safe: a dish without a selling price contributes
    /// <c>0</c> instead of raising a divide-by-zero error.
    /// </para>
    /// </summary>
    private static readonly Expression<Func<Menu, decimal>> FoodCostRatioKey =
        m => (m.FoodCost ?? 0m) / (m.Price > 0m ? (m.Price ?? 0m) : 1m) * 100m;

    /// <summary>
    /// The <b>rounded</b> food-cost percentage of a dish — the very figure the master item
    /// publishes — as a translatable SQL expression (spec §7.5 / RSK-029).
    /// <para>
    /// <c>Math.Round(x, 2)</c> translates to SQL Server's <c>ROUND(x, 2)</c>, which rounds
    /// <b>away from zero</b> — exactly the <see cref="MidpointRounding.AwayFromZero"/> the
    /// shared <c>MenuCostingCalculator</c> uses. The two-argument <c>Math.Round</c> in C# would
    /// be half-to-even, so this expression must never be evaluated in memory: the in-memory
    /// mirror of the master reads uses the explicit
    /// <see cref="MidpointRounding.AwayFromZero"/>.
    /// </para>
    /// <para>
    /// The margin-alert filter and the KPI-24 count are both built from this expression, so
    /// they can never disagree with each other or with the dish's own card.
    /// </para>
    /// </summary>
    private static readonly Expression<Func<Menu, decimal>> RoundedFoodCostPercentageKey =
        m => Math.Round((m.FoodCost ?? 0m) / (m.Price > 0m ? (m.Price ?? 0m) : 1m) * 100m, 2);

    /// <summary>
    /// The gross-margin percentage of a dish, as a translatable SQL expression:
    /// <c>(Price − FoodCost) ÷ Price × 100</c> with the same zero-price guard.
    /// </summary>
    private static readonly Expression<Func<Menu, decimal>> GrossMarginPercentageKey =
        m => (m.Price > 0m ? (m.Price ?? 0m) - (m.FoodCost ?? 0m) : 0m)
             / (m.Price > 0m ? (m.Price ?? 0m) : 1m) * 100m;

    /// <summary>
    /// The paged master read. The filtering, the ordering and the aggregation are done by SQL
    /// Server; only the derived and rounded figures are composed in the Application layer.
    /// </summary>
    public async Task<MenuMasterDataSet> GetMenuMasterDataSetAsync(
        MenuMasterFilter filter,
        CancellationToken cancellationToken = default)
    {
        var filtered = ApplyFilters(_context.Menus.AsNoTracking(), filter);

        // The aggregates describe the WHOLE filtered set, not the current page.
        var totalCount = await filtered.CountAsync(cancellationToken);
        var costedCount = await filtered.CountAsync(m => m.FoodCost > 0m, cancellationToken);

        // The margin population: the dishes that carry both a selling price and a food cost.
        // It is the same population as KPI-15, so the master and the dashboard reconcile.
        var marginPopulation = filtered.Where(m => m.Price > 0m && m.FoodCost > 0m);
        var marginPopulationCount = await marginPopulation.CountAsync(cancellationToken);
        var revenueAmount = await marginPopulation.SumAsync(m => (decimal?)m.Price, cancellationToken) ?? 0m;
        var foodCostAmount = await marginPopulation.SumAsync(m => (decimal?)m.FoodCost, cancellationToken) ?? 0m;

        // KPI-24: the dishes of the filtered set that exceed the configured target.
        var marginAlertCount = await filtered.CountAsync(
            FoodCostPercentageAbove(filter.TargetFoodCostPercentage),
            cancellationToken);

        // KPI-23: the margin figures per course, over the filtered set's margin population.
        var courseMargins = await marginPopulation
            .GroupBy(m => m.CourseId)
            .Select(g => new MenuCourseMarginAggregate
            {
                CourseId = g.Key,
                RevenueAmount = g.Sum(m => m.Price) ?? 0m,
                FoodCostAmount = g.Sum(m => m.FoodCost) ?? 0m,
                DishCount = g.Count()
            })
            .ToListAsync(cancellationToken);

        // The page rows, ordered in SQL with a stable tiebreak on the dish code. The offset is
        // computed in 64 bits and clamped, so an absurd page number can never overflow into a
        // negative skip.
        var offset = ((long)filter.Page - 1) * filter.PageSize;
        var skip = (int)Math.Min(offset, int.MaxValue);

        var rows = await ApplySort(filtered, filter.Sort)
            .Skip(skip)
            .Take(filter.PageSize)
            .Select(m => new MenuMasterRow
            {
                MenuId = m.MenuId,
                MenuName = m.MenuName ?? string.Empty,
                CourseId = m.CourseId,
                CourseName = m.CourseCategory != null ? m.CourseCategory.CourseName : null,
                Note = m.Note,
                Image = m.Image,
                Price = m.Price,
                FoodCost = m.FoodCost
            })
            .ToListAsync(cancellationToken);

        return new MenuMasterDataSet
        {
            Rows = rows,
            Aggregate = new MenuMasterAggregate
            {
                TotalCount = totalCount,
                CostedCount = costedCount,
                MarginPopulationCount = marginPopulationCount,
                RevenueAmount = revenueAmount,
                FoodCostAmount = foodCostAmount
            },
            CourseMargins = courseMargins,
            Courses = await GetCourseScopeAsync(filter, cancellationToken),
            MarginAlertCount = marginAlertCount
        };
    }

    /// <summary>
    /// The whole filtered result set, ordered by the requested sort and never paged — the source
    /// of the XLSX export (spec §6.3).
    /// </summary>
    public async Task<List<MenuMasterRow>> GetMenuMasterRowsAsync(
        MenuMasterFilter filter,
        CancellationToken cancellationToken = default)
    {
        var filtered = ApplyFilters(_context.Menus.AsNoTracking(), filter);

        return await ApplySort(filtered, filter.Sort)
            .Select(m => new MenuMasterRow
            {
                MenuId = m.MenuId,
                MenuName = m.MenuName ?? string.Empty,
                CourseId = m.CourseId,
                CourseName = m.CourseCategory != null ? m.CourseCategory.CourseName : null,
                Note = m.Note,
                Image = m.Image,
                Price = m.Price,
                FoodCost = m.FoodCost
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> CourseExistsAsync(int courseId, CancellationToken cancellationToken = default)
    {
        return await _context.CourseCategories
            .AnyAsync(c => c.CourseId == courseId, cancellationToken);
    }

    /// <summary>
    /// The course tab strip: the scope's total and one entry per course category with its live
    /// dish count.
    /// <para>
    /// The scope is <b>everything except the course filter</b> — the search and the status
    /// filters apply. The tab counts therefore narrow with the search, and the total is the
    /// exact "All Dishes (n)" count (spec §6.1).
    /// </para>
    /// </summary>
    private async Task<MenuCoursesResponse> GetCourseScopeAsync(
        MenuMasterFilter filter,
        CancellationToken cancellationToken)
    {
        var scope = ApplyScopeFilters(_context.Menus.AsNoTracking(), filter);

        var total = await scope.CountAsync(cancellationToken);

        var counts = await scope
            .Where(m => m.CourseId != null)
            .GroupBy(m => m.CourseId)
            .Select(g => new { CourseId = g.Key!.Value, DishCount = g.Count() })
            .ToListAsync(cancellationToken);

        var courses = await _context.CourseCategories
            .AsNoTracking()
            .OrderBy(c => c.CourseId)
            .Select(c => new { c.CourseId, c.CourseName })
            .ToListAsync(cancellationToken);

        var countByCourse = counts.ToDictionary(c => c.CourseId, c => c.DishCount);

        return new MenuCoursesResponse
        {
            Total = total,
            Items = courses
                .Select(c => new MenuCourseCountResponse
                {
                    CourseId = c.CourseId,
                    CourseName = c.CourseName,
                    DishCount = countByCourse.TryGetValue(c.CourseId, out var dishCount) ? dishCount : 0
                })
                .ToList()
        };
    }

    /// <summary>
    /// The filters of the master read. The soft-delete exclusion is scoped to these queries only
    /// — no global query filter is introduced (DEC-045, DEC-050, spec §7.2).
    /// <para>
    /// This is the <b>summary scope</b>: the search, the status filter and the course filter.
    /// </para>
    /// </summary>
    private static IQueryable<Menu> ApplyFilters(IQueryable<Menu> query, MenuMasterFilter filter)
    {
        query = ApplyScopeFilters(query, filter);

        if (filter.CourseId.HasValue)
        {
            query = query.Where(m => m.CourseId == filter.CourseId.Value);
        }

        return query;
    }

    /// <summary>
    /// The <b>tab-strip scope</b>: the search and the status filter, but <b>not</b> the course
    /// filter — so the course tab counts stay stable while a course is selected, while they do
    /// narrow with the search (spec §6.1).
    /// </summary>
    private static IQueryable<Menu> ApplyScopeFilters(IQueryable<Menu> query, MenuMasterFilter filter)
    {
        query = query.Where(m => !m.IsDeleted);

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            // A case-insensitive substring over the dish code, the dish name and the note.
            var term = filter.Search.ToLower();
            query = query.Where(m =>
                m.MenuId.ToLower().Contains(term) ||
                m.MenuName.ToLower().Contains(term) ||
                (m.Note != null && m.Note.ToLower().Contains(term)));
        }

        switch (filter.Status)
        {
            case MenuMasterStatus.Costed:
                query = query.Where(m => m.FoodCost > 0m);
                break;

            case MenuMasterStatus.Uncosted:
                query = query.Where(m => m.FoodCost == null || m.FoodCost <= 0m);
                break;

            case MenuMasterStatus.MarginAlert:
                query = query.Where(FoodCostPercentageAbove(filter.TargetFoodCostPercentage));
                break;
        }

        return query;
    }

    /// <summary>
    /// The ordering of the master read. Every sort carries a stable tiebreak on the dish code, so
    /// two dishes with the same key never swap places between two requests (spec §10.1 M8).
    /// </summary>
    private static IQueryable<Menu> ApplySort(IQueryable<Menu> query, MenuMasterSort sort) => sort switch
    {
        MenuMasterSort.FoodCostDesc => query.OrderByDescending(FoodCostRatioKey).ThenBy(m => m.MenuId),
        MenuMasterSort.PriceDesc => query.OrderByDescending(m => m.Price).ThenBy(m => m.MenuId),
        MenuMasterSort.PriceAsc => query.OrderBy(m => m.Price).ThenBy(m => m.MenuId),
        MenuMasterSort.MarginDesc => query.OrderBy(GrossMarginPercentageKey).ThenBy(m => m.MenuId),
        MenuMasterSort.NameAsc => query.OrderBy(m => m.MenuName).ThenBy(m => m.MenuId),
        _ => query.OrderBy(FoodCostRatioKey).ThenBy(m => m.MenuId)
    };

    /// <summary>
    /// The margin-alert predicate, built from the very same <b>rounded</b> expression the dish's
    /// own card publishes, so the status filter, the KPI-24 count and the card can never
    /// disagree (spec §7.5 / RSK-029).
    /// </summary>
    private static Expression<Func<Menu, bool>> FoodCostPercentageAbove(decimal targetPercentage)
    {
        var key = RoundedFoodCostPercentageKey;

        return Expression.Lambda<Func<Menu, bool>>(
            Expression.GreaterThan(key.Body, Expression.Constant(targetPercentage)),
            key.Parameters);
    }

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return await _context.SaveChangesAsync(cancellationToken);
    }
}
