namespace SalsCaterSuite.Application.Common.Interfaces;

using SalsCaterSuite.Application.Common.Models;
using SalsCaterSuite.Application.Features.Menus.DTOs;
using SalsCaterSuite.Application.Features.Menus.Models;
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

    /// <summary>
    /// True when a course category with this id exists. The menu write commands use it to reject
    /// an unknown course id before anything is persisted (spec §6.4, AC-M20).
    /// </summary>
    Task<bool> CourseExistsAsync(int courseId, CancellationToken cancellationToken = default);

    /// <summary>
    /// The Menu &amp; Food Costing Master read (spec §6.1): the requested page, the aggregate
    /// figures of the <b>whole filtered set</b>, the per-course margin figures, the
    /// catalogue-wide course counts and the margin-alert count.
    /// <para>
    /// The filter is already normalised by the Application layer (the paging is clamped, the
    /// status and the sort have fallen back to their defaults), so this contract carries no
    /// policy of its own.
    /// </para>
    /// <para>
    /// Soft-deleted menus are excluded. The exclusion is scoped to the menu master's own
    /// queries — no global query filter is introduced (DEC-045, DEC-050, spec §7.2).
    /// </para>
    /// </summary>
    Task<MenuMasterDataSet> GetMenuMasterDataSetAsync(
        MenuMasterFilter filter,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The <b>whole</b> filtered result set of the Menu &amp; Food Costing Master, ordered by the
    /// requested sort and never paged. The XLSX export is the current filtered result set, not
    /// the current page (spec §6.3).
    /// </summary>
    Task<List<MenuMasterRow>> GetMenuMasterRowsAsync(
        MenuMasterFilter filter,
        CancellationToken cancellationToken = default);

    Task AddAsync(Menu menu, CancellationToken cancellationToken = default);

    /// <summary>
    /// A physical delete of a menu row.
    /// <para>
    /// The menu feature does <b>not</b> use it: <c>DELETE /api/menus/{id}</c> is a soft delete
    /// (DEC-063) — the dish is flagged, never destroyed. The affordance stays for an
    /// owner-approved purge of a dish the kitchen has retired.
    /// </para>
    /// </summary>
    void Remove(Menu menu);

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
