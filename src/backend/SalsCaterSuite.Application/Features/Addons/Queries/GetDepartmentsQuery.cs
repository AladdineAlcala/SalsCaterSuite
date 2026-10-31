using SalsCaterSuite.Application.Common.Interfaces;
using SalsCaterSuite.Application.Common.Models;
using SalsCaterSuite.Application.Features.Addons.DTOs.Request;
using SalsCaterSuite.Application.Features.Addons.DTOs.Response;

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

    public async Task<Result<List<DepartmentResponse>>> ExecuteAsync(
        CancellationToken cancellationToken = default)
    {
        var rows = await _departments.GetAllAsync(cancellationToken);

        return Result<List<DepartmentResponse>>.Success(rows
            .Select(department => new DepartmentResponse
            {
                DeptId = department.DeptId,
                DeptName = department.DeptName
            })
            .ToList());
    }
}
