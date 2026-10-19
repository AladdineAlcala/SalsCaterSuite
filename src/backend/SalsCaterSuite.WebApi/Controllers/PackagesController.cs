using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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
    private readonly CreatePackageCommand _createPackage;
    private readonly UpdatePackageCommand _updatePackage;
    private readonly TogglePackageActiveCommand _toggleActive;

    public PackagesController(
        GetPackagesQuery getPackages,
        GetPackageByIdQuery getPackageById,
        CreatePackageCommand createPackage,
        UpdatePackageCommand updatePackage,
        TogglePackageActiveCommand toggleActive)
    {
        _getPackages = getPackages;
        _getPackageById = getPackageById;
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

    /// <summary>GET /api/packages/{id} — Get package by ID</summary>
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken)
    {
        var result = await _getPackageById.ExecuteAsync(id, cancellationToken);
        return result.Succeeded ? Ok(result.Data) : NotFound();
    }

    /// <summary>POST /api/packages — Create new package</summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreatePackageRequestDto request, CancellationToken cancellationToken)
    {
        var result = await _createPackage.ExecuteAsync(request, cancellationToken);
        if (!result.Succeeded)
        {
            return BadRequest(new { errors = result.Errors });
        }

        return CreatedAtAction(nameof(GetById), new { id = result.Data }, new { packageId = result.Data });
    }

    /// <summary>PUT /api/packages/{id} — Update package</summary>
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] CreatePackageRequestDto request, CancellationToken cancellationToken)
    {
        var result = await _updatePackage.ExecuteAsync(id, request, cancellationToken);
        return result.Succeeded ? NoContent() : NotFound();
    }

    /// <summary>PATCH /api/packages/{id}/toggle-active — Toggle active/inactive</summary>
    [HttpPatch("{id:int}/toggle-active")]
    public async Task<IActionResult> ToggleActive(int id, CancellationToken cancellationToken)
    {
        var result = await _toggleActive.ExecuteAsync(id, cancellationToken);
        return result.Succeeded ? Ok(new { isActive = result.Data }) : NotFound();
    }
}
