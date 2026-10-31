using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SalsCaterSuite.Application.Features.Packages;
using SalsCaterSuite.Application.Features.Packages.Commands;
using SalsCaterSuite.Application.Features.Packages.DTOs.Request;
using SalsCaterSuite.Application.Features.Packages.DTOs.Response;
using SalsCaterSuite.Application.Features.Packages.Models;
using SalsCaterSuite.Application.Features.Packages.Queries;

namespace SalsCaterSuite.WebApi.Controllers;

/// <summary>
/// The municipality catalogue (REQ-PKG-02, contract §13.9 .. §13.11).
/// <para>
/// Every endpoint requires a bearer token and carries <b>no role policy</b> (BR-12), exactly as
/// <see cref="PackagesController"/> does. The server enforces the token requirement; the client
/// hides nothing the server would allow.
/// </para>
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AreasController : ControllerBase
{
    private readonly GetAreasQuery _getAreas;
    private readonly CreateAreaCommand _createArea;
    private readonly UpdateAreaCommand _updateArea;

    public AreasController(
        GetAreasQuery getAreas,
        CreateAreaCommand createArea,
        UpdateAreaCommand updateArea)
    {
        _getAreas = getAreas;
        _createArea = createArea;
        _updateArea = updateArea;
    }

    /// <summary>GET /api/areas — the municipality catalogue (contract §13.9).</summary>
    [HttpGet]
    [ProducesResponseType(typeof(AreaResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var result = await _getAreas.ExecuteAsync(cancellationToken);
        return Ok(result.Data);
    }

    /// <summary>POST /api/areas — add a municipality (contract §13.10).</summary>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] AreaRequest request, CancellationToken cancellationToken)
    {
        var result = await _createArea.ExecuteAsync(request, cancellationToken);

        if (!result.Succeeded)
        {
            // A rejected payload is a validation failure (400) and nothing is persisted (BR-11).
            return BadRequest(new { errors = result.Errors });
        }

        return CreatedAtAction(nameof(GetAll), new { areaId = result.Data }, new { areaId = result.Data });
    }

    /// <summary>PUT /api/areas/{id} — update a municipality (contract §13.11).</summary>
    [HttpPut("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(
        int id,
        [FromBody] AreaRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _updateArea.ExecuteAsync(id, request, cancellationToken);

        if (result.Succeeded)
        {
            return NoContent();
        }

        // A rejected payload is a validation failure (400); a missing municipality is a 404. The
        // two are distinguished by the result type, never by the message text.
        return result is AreaNotFoundResult
            ? NotFound(new { errors = result.Errors })
            : BadRequest(new { errors = result.Errors });
    }
}
