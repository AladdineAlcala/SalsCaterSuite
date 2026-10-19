namespace SalsCaterSuite.Infrastructure.Persistence.Repositories;

using Microsoft.EntityFrameworkCore;
using SalsCaterSuite.Application.Common.Interfaces;
using SalsCaterSuite.Domain.Entities;

/// <summary>
/// EF Core implementation of the course-category repository (DEC-037). The seeding routine
/// reaches <c>CourseCategory</c> through this contract instead of the raw
/// <c>DbSet</c>, so all EF Core knowledge stays inside the repositories layer.
/// </summary>
public class CourseCategoryRepository : ICourseCategoryRepository
{
    private readonly SalsCaterSuiteDbContext _context;

    public CourseCategoryRepository(SalsCaterSuiteDbContext context)
    {
        _context = context;
    }

    public async Task<bool> ExistsByCourseNameAsync(string courseName, CancellationToken cancellationToken = default)
    {
        return await _context.CourseCategories
            .AnyAsync(c => c.CourseName == courseName, cancellationToken);
    }

    public async Task AddAsync(CourseCategory courseCategory, CancellationToken cancellationToken = default)
    {
        await _context.CourseCategories.AddAsync(courseCategory, cancellationToken);
    }

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return await _context.SaveChangesAsync(cancellationToken);
    }
}
