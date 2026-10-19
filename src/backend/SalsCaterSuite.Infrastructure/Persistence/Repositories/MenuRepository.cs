namespace SalsCaterSuite.Infrastructure.Persistence.Repositories;

using Microsoft.EntityFrameworkCore;
using SalsCaterSuite.Application.Common.Interfaces;
using SalsCaterSuite.Application.Common.Models;
using SalsCaterSuite.Application.Features.Menus.DTOs;
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

    public async Task<Menu?> GetByIdAsync(string menuId, CancellationToken cancellationToken = default)
    {
        return await _context.Menus
            .FirstOrDefaultAsync(m => m.MenuId == menuId, cancellationToken);
    }

    public async Task<bool> ExistsAsync(string menuId, CancellationToken cancellationToken = default)
    {
        return await _context.Menus
            .AnyAsync(m => m.MenuId == menuId, cancellationToken);
    }

    public async Task<IReadOnlyList<Menu>> GetOrderedByNameAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Menus
            .OrderBy(m => m.MenuName)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<MenuDto>> GetProjectionListAsync(int? courseId, CancellationToken cancellationToken = default)
    {
        IQueryable<Menu> query = _context.Menus.AsNoTracking()
            .Include(m => m.CourseCategory);

        if (courseId.HasValue)
            query = query.Where(m => m.CourseId == courseId.Value);

        return await query
            .OrderBy(m => m.CourseId).ThenBy(m => m.MenuName)
            .Select(m => new MenuDto
            {
                MenuId = m.MenuId,
                CourseId = m.CourseId,
                CourseName = m.CourseCategory != null ? m.CourseCategory.CourseName : null,
                MenuName = m.MenuName ?? string.Empty,
                Note = m.Note,
                Price = m.Price,
                FoodCost = m.FoodCost
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<MenuDto?> GetProjectionByIdAsync(string menuId, CancellationToken cancellationToken = default)
    {
        return await _context.Menus
            .AsNoTracking()
            .Include(m => m.CourseCategory)
            .Where(m => m.MenuId == menuId)
            .Select(m => new MenuDto
            {
                MenuId = m.MenuId,
                CourseId = m.CourseId,
                CourseName = m.CourseCategory != null ? m.CourseCategory.CourseName : null,
                MenuName = m.MenuName ?? string.Empty,
                Note = m.Note,
                Price = m.Price,
                FoodCost = m.FoodCost
            })
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<List<CourseDto>> GetCourseProjectionsAsync(CancellationToken cancellationToken = default)
    {
        return await _context.CourseCategories
            .AsNoTracking()
            .OrderBy(c => c.CourseId)
            .Select(c => new CourseDto { CourseId = c.CourseId, CourseName = c.CourseName })
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

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return await _context.SaveChangesAsync(cancellationToken);
    }
}
