using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SalsCaterSuite.Application.Features.Packages;
using SalsCaterSuite.Application.Features.Packages.Commands;
using SalsCaterSuite.Application.Features.Packages.DTOs;
using SalsCaterSuite.Application.Features.Packages.Queries;

namespace SalsCaterSuite.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PackagesController : ControllerBase
{
    private readonly GetPackagesQuery _getPackages;
    private readonly GetPackageByIdQuery _getPackageById;
    private readonly GetPackageCatalogueSummaryQuery _getCatalogueSummary;
    private readonly CreatePackageCommand _createPackage;
    private readonly UpdatePackageCommand _updatePackage;
    private readonly TogglePackageActiveCommand _toggleActive;

    public PackagesController(
        GetPackagesQuery getPackages,
        GetPackageByIdQuery getPackageById,
        GetPackageCatalogueSummaryQuery getCatalogueSummary,
        CreatePackageCommand createPackage,
        UpdatePackageCommand updatePackage,
        TogglePackageActiveCommand toggleActive)
    {
        _getPackages = getPackages;
        _getPackageById = getPackageById;
        _getCatalogueSummary = getCatalogueSummary;
        _createPackage = createPackage;
        _updatePackage = updatePackage;
        _toggleActive = toggleActive;
    }

    /// <summary>GET /api/packages — List all catering packages</summary>
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] bool? activeOnly, CancellationToken cancellationToken)
    {
        var result = await _getPackages.ExecuteAsync(activeOnly, cancellationToken);
        return Ok(result.Data);
    }

    /// <summary>
    /// GET /api/packages/summary — the catalogue aggregate (spec §3.2, DEC-068).
    /// <para>
    /// The route is a literal segment, so it can never be captured by
    /// <c>GET /api/packages/{id:int}</c>.
    /// </para>
    /// </summary>
    [HttpGet("summary")]
    [ProducesResponseType(typeof(PackageCatalogueSummaryDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSummary(CancellationToken cancellationToken)
    {
        var result = await _getCatalogueSummary.ExecuteAsync(cancellationToken);
        return Ok(result.Data);
    }

    /// <summary>GET /api/packages/{id} - Get package by ID</summary>
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken)
    {
        var result = await _getPackageById.ExecuteAsync(id, cancellationToken);
        // The 404 carries the standard { "errors": [ ... ] } envelope (contract section 7).
        // A soft-deleted tier is not part of the catalogue and answers 404 (DEC-074).
        return result.Succeeded ? Ok(result.Data) : NotFound(new { errors = result.Errors });
    }

    /// <summary>POST /api/packages — Create new package</summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreatePackageRequestDto request, CancellationToken cancellationToken)
    {
        var result = await _createPackage.ExecuteAsync(request, cancellationToken);
        if (!result.Succeeded)
        {
            // A rejected payload is a validation failure (400). Both failures use the uniform
            // { "errors": [ ... ] } envelope (contract §7, spec §3.3).
            return BadRequest(new { errors = result.Errors });
        }

        return CreatedAtAction(nameof(GetById), new { id = result.Data }, new { packageId = result.Data });
    }

    /// <summary>PUT /api/packages/{id} — Update package</summary>
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] CreatePackageRequestDto request, CancellationToken cancellationToken)
    {
        var result = await _updatePackage.ExecuteAsync(id, request, cancellationToken);
        if (result.Succeeded)
        {
            return NoContent();
        }

        // A rejected payload is a validation failure (400); a missing tier is a 404. The two are
        // distinguished by the result type, never by the message text.
        return result is PackageNotFoundResult
            ? NotFound(new { errors = result.Errors })
            : BadRequest(new { errors = result.Errors });
    }

    /// <summary>PATCH /api/packages/{id}/toggle-active - Toggle active/inactive</summary>
    [HttpPatch("{id:int}/toggle-active")]
    public async Task<IActionResult> ToggleActive(int id, CancellationToken cancellationToken)
    {
        var result = await _toggleActive.ExecuteAsync(id, cancellationToken);
        // The 404 carries the standard { "errors": [ ... ] } envelope (contract section 7).
        return result.Succeeded
            ? Ok(new { isActive = result.Data })
            : NotFound(new { errors = result.Errors });
    }
}
