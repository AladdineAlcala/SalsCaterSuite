namespace SalsCaterSuite.Application.Common.Interfaces;

using SalsCaterSuite.Domain.Entities;

/// <summary>
/// Course-category reference-data persistence (DEC-037). Expressed in terms of the
/// <see cref="CourseCategory"/> domain entity only — no EF Core type leaks through this
/// contract.
/// <para>
/// Used by the controlled seeding routine (DEC-018): add-if-absent on the natural key
/// (course name) and never update an existing row. The read projection served by
/// <c>GET /api/menus/courses</c> stays on <see cref="IMenuRepository"/>, which owns the
/// Menu feature.
/// </para>
/// <para>
/// Atomicity: every repository implementation shares the request-scoped
/// <c>SalsCaterSuiteDbContext</c>, so a single <see cref="SaveChangesAsync"/> commits the
/// entire pending change set.
/// </para>
/// </summary>
public interface ICourseCategoryRepository
{
    /// <summary>True when a course category with this natural key already exists.</summary>
    Task<bool> ExistsByCourseNameAsync(string courseName, CancellationToken cancellationToken = default);

    Task AddAsync(CourseCategory courseCategory, CancellationToken cancellationToken = default);

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
