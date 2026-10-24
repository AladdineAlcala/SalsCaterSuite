using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SalsCaterSuite.Application.Features.Addons.DTOs;
using SalsCaterSuite.Application.Features.Addons.Queries;

namespace SalsCaterSuite.WebApi.Controllers;

/// <summary>
/// The read-only department lookup (the owner's 2026-10-24 add-on administration request). The
/// add-on form needs the list for its optional selector; the department master itself has no
/// write surface in this cycle, and it ships empty (BR-13).
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class DepartmentsController : ControllerBase
{
    private readonly GetDepartmentsQuery _getDepartments;

    public DepartmentsController(GetDepartmentsQuery getDepartments)
    {
        _getDepartments = getDepartments;
    }

    /// <summary>GET /api/departments — every non-soft-deleted department, ordered by name.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(List<DepartmentDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var result = await _getDepartments.ExecuteAsync(cancellationToken);
        return Ok(result.Data);
    }
}
