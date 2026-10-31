namespace SalsCaterSuite.Infrastructure.Persistence.Repositories;

using Microsoft.EntityFrameworkCore;
using SalsCaterSuite.Application.Common.Interfaces;
using SalsCaterSuite.Application.Features.Packages.DTOs.Request;
using SalsCaterSuite.Application.Features.Packages.DTOs.Response;
using SalsCaterSuite.Application.Features.Packages.Models;
using SalsCaterSuite.Domain.Entities;

/// <summary>
/// EF Core implementation of the course-rule repository (DEC-037). The projection resolves the
/// course name in one round trip, so the ruleset read is a single statement.
/// </summary>
public class PackageCourseRuleRepository : IPackageCourseRuleRepository
{
    private readonly SalsCaterSuiteDbContext _context;

    public PackageCourseRuleRepository(SalsCaterSuiteDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// The tier's live rules, with the course name resolved.
    /// <para>
    /// The course join is a left join, so a soft-deleted course still yields the rule with an
    /// empty course name. The retired upgrade add-on join was removed with the field itself
    /// (DEC-172) — a rule no longer references the add-on master.
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
            select new PackageCourseRuleProjection
            {
                PackageCourseRuleId = rule.PackageCourseRuleId,
                PackageId = rule.PackageId,
                CourseId = rule.CourseId,
                CourseName = course != null ? course.CourseName : string.Empty,
                RequiredQuota = rule.RequiredQuota,
                PortionGrams = rule.PortionGrams,
                RuleDescription = rule.RuleDescription
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

    public async Task AddAsync(PackageCourseRule rule, CancellationToken cancellationToken = default)
    {
        await _context.PackageCourseRules.AddAsync(rule, cancellationToken);
    }

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return await _context.SaveChangesAsync(cancellationToken);
    }
}
