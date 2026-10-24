namespace SalsCaterSuite.UnitTests.Common.Fakes;

using SalsCaterSuite.Application.Common.Interfaces;
using SalsCaterSuite.Domain.Entities;

/// <summary>Hand-written fake of the read-only department lookup (DEC-037).</summary>
public sealed class FakeDepartmentRepository : IDepartmentRepository
{
    private readonly FakeSalsCaterSuiteDataStore _store;

    public FakeDepartmentRepository(FakeSalsCaterSuiteDataStore store)
    {
        _store = store;
    }

    public Task<IReadOnlyList<Department>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<Department> result = _store.Departments
            .Where(department => !department.IsDeleted)
            .OrderBy(department => department.DeptName)
            .ThenBy(department => department.DeptId)
            .ToList();

        return Task.FromResult(result);
    }

    public Task<bool> ExistsAsync(int deptId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_store.Departments.Any(
            department => department.DeptId == deptId && !department.IsDeleted));
    }
}
