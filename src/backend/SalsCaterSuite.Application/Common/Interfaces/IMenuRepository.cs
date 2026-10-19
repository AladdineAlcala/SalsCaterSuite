namespace SalsCaterSuite.Application.Common.Interfaces;

using SalsCaterSuite.Application.Common.Models;
using SalsCaterSuite.Application.Features.Menus.DTOs;
using SalsCaterSuite.Domain.Entities;

/// <summary>
/// Menu aggregate persistence (DEC-037). Expressed in terms of the <see cref="Menu"/>
/// domain entity and Application projections only — no EF Core type leaks through this
/// contract. Also serves the menu-costing reads and the course-category lookup, which
/// belong to the same Menu feature.
/// <para>
/// Atomicity: every repository implementation shares the request-scoped
/// <c>SalsCaterSuiteDbContext</c>, so a single <see cref="SaveChangesAsync"/> commits the
/// entire pending change set.
/// </para>
/// </summary>
public interface IMenuRepository
{
    Task<Menu?> GetByIdAsync(string menuId, CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(string menuId, CancellationToken cancellationToken = default);

    /// <summary>All menus ordered by name — the menu-costing list projection source.</summary>
    Task<IReadOnlyList<Menu>> GetOrderedByNameAsync(CancellationToken cancellationToken = default);

    /// <summary>Menu list projection (course name included), optionally filtered by course.</summary>
    Task<List<MenuDto>> GetProjectionListAsync(int? courseId, CancellationToken cancellationToken = default);

    /// <summary>Single-menu projection (course name included).</summary>
    Task<MenuDto?> GetProjectionByIdAsync(string menuId, CancellationToken cancellationToken = default);

    /// <summary>Course-category projection used by GET /api/menus/courses.</summary>
    Task<List<CourseDto>> GetCourseProjectionsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// The menu-catalogue costing aggregate used by the operations dashboard: the total menu
    /// count, the costed-menu count, and the catalogue price and food-cost sums over the
    /// margin population (REQ-DSH-01, spec §7 R4, KPI-14, KPI-15).
    /// <para>
    /// Soft-deleted menus are excluded. The exclusion is scoped to this dashboard query — no
    /// global query filter is applied (DEC-038, spec §6.13).
    /// </para>
    /// </summary>
    Task<MenuCostingAggregate> GetDashboardCostingAggregateAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// The per-course food-cost breakdown used by the operations dashboard: one row per
    /// course that has at least one costed menu, with the course's food-cost sum and menu
    /// count (REQ-DSH-01, spec §7 R4, KPI-16). Soft-deleted menus are excluded.
    /// </summary>
    Task<IReadOnlyList<MenuCourseCostAggregate>> GetDashboardCourseCostAggregatesAsync(CancellationToken cancellationToken = default);

    Task AddAsync(Menu menu, CancellationToken cancellationToken = default);

    void Remove(Menu menu);

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
