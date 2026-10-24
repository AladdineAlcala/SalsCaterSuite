using SalsCaterSuite.Application.Common.Interfaces;
using SalsCaterSuite.Application.Common.Models;
using SalsCaterSuite.Application.Features.Addons.DTOs;

namespace SalsCaterSuite.Application.Features.Addons.Queries;

/// <summary>
/// <c>GET /api/departments</c> — the read-only department lookup the add-on form uses for its
/// optional selector. The master ships empty (BR-13), so an empty list is a valid answer.
/// </summary>
public class GetDepartmentsQuery
{
    private readonly IDepartmentRepository _departments;

    public GetDepartmentsQuery(IDepartmentRepository departments)
    {
        _departments = departments;
    }

    public async Task<Result<List<DepartmentDto>>> ExecuteAsync(
        CancellationToken cancellationToken = default)
    {
        var rows = await _departments.GetAllAsync(cancellationToken);

        return Result<List<DepartmentDto>>.Success(rows
            .Select(department => new DepartmentDto
            {
                DeptId = department.DeptId,
                DeptName = department.DeptName
            })
            .ToList());
    }
}
