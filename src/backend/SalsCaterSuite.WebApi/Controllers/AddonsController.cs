using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SalsCaterSuite.Application.Features.Addons;
using SalsCaterSuite.Application.Features.Addons.Commands;
using SalsCaterSuite.Application.Features.Addons.DTOs;
using SalsCaterSuite.Application.Features.Addons.Queries;
using SalsCaterSuite.Application.Features.Bookings.DTOs;
using SalsCaterSuite.Application.Features.Bookings.Queries;

namespace SalsCaterSuite.WebApi.Controllers;

/// <summary>
/// The add-on master (REQ-BKG-07, contract §16). The booking picker's read is active-only; the
/// administration surface added on 2026-10-24 exposes the full CRUD under
/// <c>/api/addons/manage</c> and the write routes, so the two populations never mix.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AddonsController : ControllerBase
{
    private readonly GetAddonMasterQuery _getAddons;
    private readonly GetAddonCategoriesQuery _getCategories;
    private readonly GetAddonManagementListQuery _getManagedAddons;
    private readonly GetAddonManagementDetailQuery _getManagedAddon;
    private readonly CreateAddonCommand _createAddon;
    private readonly UpdateAddonCommand _updateAddon;
    private readonly DeleteAddonCommand _deleteAddon;

    public AddonsController(
        GetAddonMasterQuery getAddons,
        GetAddonCategoriesQuery getCategories,
        GetAddonManagementListQuery getManagedAddons,
        GetAddonManagementDetailQuery getManagedAddon,
        CreateAddonCommand createAddon,
        UpdateAddonCommand updateAddon,
        DeleteAddonCommand deleteAddon)
    {
        _getAddons = getAddons;
        _getCategories = getCategories;
        _getManagedAddons = getManagedAddons;
        _getManagedAddon = getManagedAddon;
        _createAddon = createAddon;
        _updateAddon = updateAddon;
        _deleteAddon = deleteAddon;
    }

    /// <summary>
    /// GET /api/addons — the active add-ons, optionally narrowed to one category.
    /// <para>
    /// <c>headcount</c> is <b>required</b> (contract §16.1): it is the figure
    /// <c>lineAmountForBooking</c> is computed for, so the client never performs the
    /// multiplication itself. It has no default — a default of one guest would publish a
    /// per-guest unit price indistinguishable from a genuine one-guest booking (BR-20).
    /// </para>
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(List<AddonMasterItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetAll(
        [FromQuery] int? categoryId,
        [FromQuery] int? headcount,
        CancellationToken cancellationToken)
    {
        if (headcount is null or < 1)
        {
            return BadRequest(new
            {
                errors = new[] { "headcount is required and must be at least 1." }
            });
        }

        var result = await _getAddons.ExecuteAsync(categoryId, headcount.Value, cancellationToken);
        return Ok(result.Data);
    }

    /// <summary>GET /api/addons/categories — the add-on category master (contract §16.2).</summary>
    [HttpGet("categories")]
    [ProducesResponseType(typeof(List<AddonCategoryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCategories(CancellationToken cancellationToken)
    {
        var result = await _getCategories.ExecuteAsync(cancellationToken);
        return Ok(result.Data);
    }

    /// <summary>
    /// GET /api/addons/manage — the administration list: every non-soft-deleted add-on, active or
    /// inactive, with its category and department (the 2026-10-24 request).
    /// </summary>
    [HttpGet("manage")]
    [ProducesResponseType(typeof(List<AddonAdminItemDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetManaged(CancellationToken cancellationToken)
    {
        var result = await _getManagedAddons.ExecuteAsync(cancellationToken);
        return Ok(result.Data);
    }

    /// <summary>GET /api/addons/manage/{id} — one add-on for the administration form.</summary>
    [HttpGet("manage/{id:int}")]
    [ProducesResponseType(typeof(AddonAdminItemDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetManagedById(int id, CancellationToken cancellationToken)
    {
        var result = await _getManagedAddon.ExecuteAsync(id, cancellationToken);
        return result.Succeeded
            ? Ok(result.Data)
            : NotFound(new { errors = result.Errors });
    }

    /// <summary>POST /api/addons — create an add-on.</summary>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create(
        [FromBody] AddonUpsertRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _createAddon.ExecuteAsync(request, cancellationToken);

        return result.Succeeded
            ? CreatedAtAction(
                nameof(GetManagedById),
                new { id = result.Data },
                new { addonId = result.Data })
            : BadRequest(new { errors = result.Errors });
    }

    /// <summary>PUT /api/addons/{id} — update an add-on.</summary>
    [HttpPut("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(
        int id,
        [FromBody] AddonUpsertRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _updateAddon.ExecuteAsync(id, request, cancellationToken);

        if (result.Succeeded)
        {
            return NoContent();
        }

        // A rejected payload is a 400; a missing or soft-deleted add-on is a 404. The two are
        // distinguished by the result type, never by the message text.
        return result is AddonNotFoundResult
            ? NotFound(new { errors = result.Errors })
            : BadRequest(new { errors = result.Errors });
    }

    /// <summary>DELETE /api/addons/{id} — soft-delete an add-on.</summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var result = await _deleteAddon.ExecuteAsync(id, cancellationToken);

        return result.Succeeded
            ? NoContent()
            : NotFound(new { errors = result.Errors });
    }
}
