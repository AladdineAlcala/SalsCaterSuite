namespace SalsCaterSuite.Infrastructure.Persistence.Repositories;

using Microsoft.EntityFrameworkCore;
using SalsCaterSuite.Application.Common.Interfaces;
using SalsCaterSuite.Domain.Entities;

/// <summary>
/// EF Core implementation of the read-only department lookup (the owner's 2026-10-24 request).
/// The master ships empty (BR-13); the add-on form uses this list for its optional selector.
/// </summary>
public class DepartmentRepository : IDepartmentRepository
{
    private readonly SalsCaterSuiteDbContext _context;

    public DepartmentRepository(SalsCaterSuiteDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<Department>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        return await _context.Departments
            .AsNoTracking()
            .Where(department => !department.IsDeleted)
            .OrderBy(department => department.DeptName)
            .ThenBy(department => department.DeptId)
            .ToListAsync(cancellationToken);
    }

    public Task<bool> ExistsAsync(int deptId, CancellationToken cancellationToken = default)
    {
        return _context.Departments
            .AsNoTracking()
            .AnyAsync(
                department => department.DeptId == deptId && !department.IsDeleted,
                cancellationToken);
    }
}
