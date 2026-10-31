namespace SalsCaterSuite.UnitTests.Common.Fakes;

using SalsCaterSuite.Application.Common.Interfaces;
using SalsCaterSuite.Application.Features.Packages.DTOs.Request;
using SalsCaterSuite.Application.Features.Packages.DTOs.Response;
using SalsCaterSuite.Application.Features.Packages.Models;
using SalsCaterSuite.Domain.Entities;

/// <summary>
/// Hand-written fake of <see cref="IPackageCourseRuleRepository"/> (DEC-037). No EF Core is
/// involved; in-place updates are visible through the shared reference-tracking store.
/// <para>
/// The projection mirrors the EF implementation exactly. The retired upgrade add-on resolution
/// was removed with the field itself (DEC-172): a rule no longer references the add-on master.
/// </para>
/// </summary>
public sealed class FakePackageCourseRuleRepository : IPackageCourseRuleRepository
{
    private readonly FakeSalsCaterSuiteDataStore _store;

    public FakePackageCourseRuleRepository(FakeSalsCaterSuiteDataStore store)
    {
        _store = store;
    }

    /// <summary>Number of successful saves, for asserting the commit boundary.</summary>
    public int SaveCount { get; private set; }

    public Task<IReadOnlyList<PackageCourseRuleProjection>> GetLiveRulesAsync(
        int packageId,
        CancellationToken cancellationToken = default)
    {
        var live = _store.PackageCourseRules
            .Where(rule => rule.PackageId == packageId && !rule.IsDeleted)
            .ToList();

        var result = live.Select(Map).ToList();

        return Task.FromResult<IReadOnlyList<PackageCourseRuleProjection>>(result);
    }

    public Task<IReadOnlyList<PackageCourseRule>> GetAllRulesAsync(
        int packageId,
        CancellationToken cancellationToken = default)
    {
        var result = _store.PackageCourseRules
            .Where(rule => rule.PackageId == packageId)
            .ToList();

        return Task.FromResult<IReadOnlyList<PackageCourseRule>>(result);
    }

    public Task<bool> LiveCourseExistsAsync(int courseId, CancellationToken cancellationToken = default)
    {
        // A soft-deleted course does not exist for the workbench (spec §3.6).
        var exists = _store.CourseCategories.Any(course => course.CourseId == courseId && !course.IsDeleted);

        return Task.FromResult(exists);
    }

    public Task AddAsync(PackageCourseRule rule, CancellationToken cancellationToken = default)
    {
        _store.PackageCourseRules.Add(rule);
        return Task.CompletedTask;
    }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        SaveCount++;

        foreach (var rule in _store.PackageCourseRules.Where(rule => rule.PackageCourseRuleId == 0).ToList())
        {
            _store.AssignPackageCourseRuleId(rule);
        }

        return Task.FromResult(1);
    }

    private PackageCourseRuleProjection Map(PackageCourseRule rule)
    {
        return new PackageCourseRuleProjection
        {
            PackageCourseRuleId = rule.PackageCourseRuleId,
            PackageId = rule.PackageId,
            CourseId = rule.CourseId,
            CourseName = _store.CourseCategories
                .FirstOrDefault(course => course.CourseId == rule.CourseId)?.CourseName ?? string.Empty,
            RequiredQuota = rule.RequiredQuota,
            PortionGrams = rule.PortionGrams,
            RuleDescription = rule.RuleDescription
        };
    }
}
