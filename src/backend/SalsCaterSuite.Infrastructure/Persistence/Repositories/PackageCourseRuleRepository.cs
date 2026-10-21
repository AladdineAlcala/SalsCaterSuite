namespace SalsCaterSuite.Infrastructure.Persistence.Repositories;

using Microsoft.EntityFrameworkCore;
using SalsCaterSuite.Application.Common.Interfaces;
using SalsCaterSuite.Application.Features.Packages.DTOs;
using SalsCaterSuite.Domain.Entities;

/// <summary>
/// EF Core implementation of the course-rule repository (DEC-037). The projection resolves the
/// course name and the upgrade add-on's details in one round trip, so the ruleset read is a
/// single statement.
/// </summary>
public class PackageCourseRuleRepository : IPackageCourseRuleRepository
{
    private readonly SalsCaterSuiteDbContext _context;

    public PackageCourseRuleRepository(SalsCaterSuiteDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// The tier's live rules, with the course name and the upgrade add-on's details resolved.
    /// <para>
    /// A soft-deleted upgrade add-on keeps its id and publishes the description, the unit price
    /// and the unit as <c>null</c> (contract §13.4) — the left join keeps the rule and the
    /// conditional nulls the add-on's fields.
    /// </para>
    /// </summary>
    public async Task<IReadOnlyList<PackageCourseRuleProjection>> GetLiveRulesAsync(
        int packageId,
        CancellationToken cancellationToken = default)
    {
        var query =
            from rule in _context.PackageCourseRules.AsNoTracking()
            where rule.PackageId == packageId && !rule.IsDeleted
            join course in _context.CourseCategories on rule.CourseId equals course.CourseId into courses
            from course in courses.DefaultIfEmpty()
            join addon in _context.AddonDetails on rule.UpgradeAddonId equals addon.AddonId into addons
            from addon in addons.DefaultIfEmpty()
            select new PackageCourseRuleProjection
            {
                PackageCourseRuleId = rule.PackageCourseRuleId,
                PackageId = rule.PackageId,
                CourseId = rule.CourseId,
                CourseName = course != null ? course.CourseName : string.Empty,
                RequiredQuota = rule.RequiredQuota,
                PortionGrams = rule.PortionGrams,
                RuleDescription = rule.RuleDescription,
                InclusionTag = rule.InclusionTag,
                UpgradeAddonId = rule.UpgradeAddonId,
                UpgradeAddonDescription = addon != null && !addon.IsDeleted ? addon.Description : null,
                UpgradeAddonUnitPrice = addon != null && !addon.IsDeleted ? addon.UnitPrice : null,
                UpgradeAddonUnit = addon != null && !addon.IsDeleted ? addon.Unit : null
            };

        var result = await query.ToListAsync(cancellationToken);

        return result;
    }

    /// <summary>
    /// Every rule row the tier holds, the soft-deleted ones included — the population the write
    /// use case matches the natural key over, so a re-added course resurrects its row (DEC-088).
    /// </summary>
    public async Task<IReadOnlyList<PackageCourseRule>> GetAllRulesAsync(
        int packageId,
        CancellationToken cancellationToken = default)
    {
        return await _context.PackageCourseRules
            .Where(rule => rule.PackageId == packageId)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// True when a <b>live</b> course category with this id exists. A soft-deleted course does
    /// not exist for the workbench's validation (spec §3.6).
    /// </summary>
    public async Task<bool> LiveCourseExistsAsync(int courseId, CancellationToken cancellationToken = default)
    {
        return await _context.CourseCategories
            .AnyAsync(course => course.CourseId == courseId && !course.IsDeleted, cancellationToken);
    }

    /// <summary>
    /// True when a <b>live</b> add-on with this id exists. A soft-deleted add-on does not exist
    /// for the workbench's validation (spec §3.6).
    /// </summary>
    public async Task<bool> LiveAddonExistsAsync(int addonId, CancellationToken cancellationToken = default)
    {
        return await _context.AddonDetails
            .AnyAsync(addon => addon.AddonId == addonId && !addon.IsDeleted, cancellationToken);
    }

    public async Task AddAsync(PackageCourseRule rule, CancellationToken cancellationToken = default)
    {
        await _context.PackageCourseRules.AddAsync(rule, cancellationToken);
    }

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return await _context.SaveChangesAsync(cancellationToken);
    }
}
