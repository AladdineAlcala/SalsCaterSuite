using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SalsCaterSuite.Application.Common.Interfaces;
using SalsCaterSuite.Application.Features.Packages.DTOs;
using SalsCaterSuite.Domain.Entities;

namespace SalsCaterSuite.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PackagesController : ControllerBase
{
    private readonly ISalsCaterSuiteDbContext _context;

    public PackagesController(ISalsCaterSuiteDbContext context)
    {
        _context = context;
    }

    /// <summary>GET /api/packages — List all catering packages</summary>
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] bool? activeOnly, CancellationToken cancellationToken)
    {
        var query = _context.Packages.AsNoTracking();

        if (activeOnly == true)
            query = query.Where(p => p.IsActive);

        var packages = await query
            .OrderBy(p => p.PackageType).ThenBy(p => p.Description)
            .Select(p => new PackageDto
            {
                PackageId = p.PackageId,
                PackageType = p.PackageType ?? string.Empty,
                Description = p.Description ?? string.Empty,
                AmountPax = p.AmountPax,
                MinPax = p.MinPax,
                IsActive = p.IsActive
            })
            .ToListAsync(cancellationToken);

        return Ok(packages);
    }

    /// <summary>GET /api/packages/{id} — Get package by ID</summary>
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken)
    {
        var package = await _context.Packages
            .AsNoTracking()
            .Where(p => p.PackageId == id)
            .Select(p => new PackageDto
            {
                PackageId = p.PackageId,
                PackageType = p.PackageType ?? string.Empty,
                Description = p.Description ?? string.Empty,
                AmountPax = p.AmountPax,
                MinPax = p.MinPax,
                IsActive = p.IsActive
            })
            .FirstOrDefaultAsync(cancellationToken);

        return package is null ? NotFound() : Ok(package);
    }

    /// <summary>POST /api/packages — Create new package</summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreatePackageRequestDto request, CancellationToken cancellationToken)
    {
        var package = new Package
        {
            PackageType = request.PackageType,
            Description = request.Description,
            AmountPax = request.AmountPax,
            MinPax = request.MinPax,
            IsActive = true
        };

        _context.Packages.Add(package);
        await _context.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(nameof(GetById), new { id = package.PackageId }, new { packageId = package.PackageId });
    }

    /// <summary>PUT /api/packages/{id} — Update package</summary>
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] CreatePackageRequestDto request, CancellationToken cancellationToken)
    {
        var package = await _context.Packages.FirstOrDefaultAsync(p => p.PackageId == id, cancellationToken);
        if (package is null) return NotFound();

        package.PackageType = request.PackageType;
        package.Description = request.Description;
        package.AmountPax = request.AmountPax;
        package.MinPax = request.MinPax;

        await _context.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    /// <summary>PATCH /api/packages/{id}/toggle-active — Toggle active/inactive</summary>
    [HttpPatch("{id:int}/toggle-active")]
    public async Task<IActionResult> ToggleActive(int id, CancellationToken cancellationToken)
    {
        var package = await _context.Packages.FirstOrDefaultAsync(p => p.PackageId == id, cancellationToken);
        if (package is null) return NotFound();

        package.IsActive = !package.IsActive;
        await _context.SaveChangesAsync(cancellationToken);

        return Ok(new { isActive = package.IsActive });
    }
}

public record CreatePackageRequestDto(
    string PackageType,
    string Description,
    decimal? AmountPax,
    int? MinPax);
