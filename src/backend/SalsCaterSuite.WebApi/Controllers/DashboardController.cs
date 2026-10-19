using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SalsCaterSuite.Application.Features.Dashboard;
using SalsCaterSuite.Application.Features.Dashboard.DTOs;
using SalsCaterSuite.Application.Features.Dashboard.Queries;

namespace SalsCaterSuite.WebApi.Controllers;

/// <summary>
/// The read-only operations-dashboard aggregate (REQ-DSH-01, contract §9).
/// <para>
/// The controller is transport-only: it validates nothing, computes nothing and owns no
/// business rule. Every figure in the payload is computed server-side by the Application
/// layer (DEC-002).
/// </para>
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class DashboardController : ControllerBase
{
    private readonly GetDashboardQuery _getDashboard;

    public DashboardController(GetDashboardQuery getDashboard)
    {
        _getDashboard = getDashboard;
    }

    /// <summary>GET /api/dashboard — the operations dashboard aggregate</summary>
    /// <param name="period">The optional reporting period, <c>YYYY-MM</c>. Defaults to the current UTC calendar month.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    [HttpGet]
    [ProducesResponseType(typeof(DashboardDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get([FromQuery] string? period, CancellationToken cancellationToken)
    {
        var result = await _getDashboard.ExecuteAsync(period, cancellationToken);

        if (result.Succeeded)
        {
            return Ok(result.Data);
        }

        // A malformed period is a validation failure (400); a "not found" style failure is
        // reported as 404. Both use the uniform { "errors": [ ... ] } envelope (contract §7).
        return result is DashboardNotFoundResult
            ? NotFound(new { errors = result.Errors })
            : BadRequest(new { errors = result.Errors });
    }
}
