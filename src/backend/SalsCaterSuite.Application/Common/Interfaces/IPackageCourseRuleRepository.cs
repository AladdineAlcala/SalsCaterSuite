namespace SalsCaterSuite.Application.Common.Interfaces;

using SalsCaterSuite.Application.Features.Packages.DTOs.Request;
using SalsCaterSuite.Application.Features.Packages.DTOs.Response;
using SalsCaterSuite.Application.Features.Packages.Models;
using SalsCaterSuite.Domain.Entities;

/// <summary>
/// The course-coverage ruleset persistence (DEC-037). Expressed in terms of the
/// <see cref="PackageCourseRule"/> domain entity and Application projections only — no EF Core
/// type leaks through this contract.
/// <para>
/// Atomicity: every repository implementation shares the request-scoped
/// <c>SalsCaterSuiteDbContext</c>, so a single <c>SaveChangesAsync</c> commits the entire
/// pending change set — a ruleset is replaced as one document (DEC-085).
/// </para>
/// </summary>
public interface IPackageCourseRuleRepository
{
    /// <summary>
    /// The tier's <b>live</b> rules, with the course name resolved. Soft-deleted rules are
    /// excluded (DEC-074).
    /// </summary>
    Task<IReadOnlyList<PackageCourseRuleProjection>> GetLiveRulesAsync(
        int packageId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// <b>Every</b> rule row the tier holds, including the soft-deleted ones. The write use case
    /// matches the natural key over this population so a re-added course <b>resurrects</b> its
    /// soft-deleted row instead of inserting a second one (DEC-088).
    /// </summary>
    Task<IReadOnlyList<PackageCourseRule>> GetAllRulesAsync(
        int packageId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// True when a <b>live</b> course category with this id exists. A soft-deleted course does
    /// not exist for the workbench's validation (spec §3.6).
    /// <para>
    /// This is deliberately <b>not</b> <c>IMenuRepository.CourseExistsAsync</c>, which is the
    /// menu write path's unfiltered check: the two have different populations and must not drift.
    /// </para>
    /// </summary>
    Task<bool> LiveCourseExistsAsync(int courseId, CancellationToken cancellationToken = default);

    Task AddAsync(PackageCourseRule rule, CancellationToken cancellationToken = default);

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
