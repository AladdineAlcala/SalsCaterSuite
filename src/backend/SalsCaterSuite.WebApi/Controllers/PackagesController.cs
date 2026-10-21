using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QuestPDF.Fluent;
using SalsCaterSuite.Application.Features.Packages;
using SalsCaterSuite.Application.Features.Packages.Commands;
using SalsCaterSuite.Application.Features.Packages.DTOs;
using SalsCaterSuite.Application.Features.Packages.Queries;
using SalsCaterSuite.Infrastructure.Documents;

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

    // The Package Configuration Workbench (REQ-PKG-02, contract §13).
    private readonly GetPackageCourseRulesetQuery _getCourseRuleset;
    private readonly GetPackageAreaCoverageMatrixQuery _getCoverageMatrix;
    private readonly PreviewPackageCourseRuleQuery _previewCourseRules;
    private readonly PreviewPackageLocationQuery _previewLocation;
    private readonly GetPackageSpecSheetQuery _getSpecSheet;
    private readonly ReplacePackageCourseRulesCommand _replaceCourseRules;
    private readonly ReplacePackageAreaCoverageCommand _replaceCoverage;
    private readonly AttachPackageAreaCoverageCommand _attachCoverage;

    public PackagesController(
        GetPackagesQuery getPackages,
        GetPackageByIdQuery getPackageById,
        GetPackageCatalogueSummaryQuery getCatalogueSummary,
        CreatePackageCommand createPackage,
        UpdatePackageCommand updatePackage,
        TogglePackageActiveCommand toggleActive,
        GetPackageCourseRulesetQuery getCourseRuleset,
        GetPackageAreaCoverageMatrixQuery getCoverageMatrix,
        PreviewPackageCourseRuleQuery previewCourseRules,
        PreviewPackageLocationQuery previewLocation,
        GetPackageSpecSheetQuery getSpecSheet,
        ReplacePackageCourseRulesCommand replaceCourseRules,
        ReplacePackageAreaCoverageCommand replaceCoverage,
        AttachPackageAreaCoverageCommand attachCoverage)
    {
        _getPackages = getPackages;
        _getPackageById = getPackageById;
        _getCatalogueSummary = getCatalogueSummary;
        _createPackage = createPackage;
        _updatePackage = updatePackage;
        _toggleActive = toggleActive;
        _getCourseRuleset = getCourseRuleset;
        _getCoverageMatrix = getCoverageMatrix;
        _previewCourseRules = previewCourseRules;
        _previewLocation = previewLocation;
        _getSpecSheet = getSpecSheet;
        _replaceCourseRules = replaceCourseRules;
        _replaceCoverage = replaceCoverage;
        _attachCoverage = attachCoverage;
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

    // ─────────────────────── the Package Configuration Workbench (REQ-PKG-02) ───────────────────────

    /// <summary>
    /// GET /api/packages/{id}/course-rules — the tier's course-coverage ruleset, its qualified
    /// pools and its planning figures (contract §13.1).
    /// </summary>
    [HttpGet("{id:int}/course-rules")]
    [ProducesResponseType(typeof(PackageCourseRulesetDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetCourseRules(int id, CancellationToken cancellationToken)
    {
        var result = await _getCourseRuleset.ExecuteAsync(id, cancellationToken);

        // A soft-deleted tier is not part of the catalogue population and answers 404
        // (DEC-074).
        return result.Succeeded
            ? Ok(result.Data)
            : NotFound(new { errors = result.Errors });
    }

    /// <summary>
    /// PUT /api/packages/{id}/course-rules — replace the tier's ruleset (contract §13.2).
    /// <para>
    /// The submitted set replaces the tier's ruleset; an entry that is not submitted is
    /// soft-deleted, and a re-added course resurrects its row (DEC-085, DEC-088).
    /// </para>
    /// </summary>
    [HttpPut("{id:int}/course-rules")]
    [ProducesResponseType(typeof(PackageCourseRulesetDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ReplaceCourseRules(
        int id,
        [FromBody] PackageCourseRulesetRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _replaceCourseRules.ExecuteAsync(id, request, cancellationToken);

        if (result.Succeeded)
        {
            return Ok(result.Data);
        }

        // A rejected payload is a validation failure (400); a missing tier is a 404. The two are
        // distinguished by the result type, never by the message text.
        return result is PackageNotFoundResult<PackageCourseRulesetDto>
            ? NotFound(new { errors = result.Errors })
            : BadRequest(new { errors = result.Errors });
    }

    /// <summary>
    /// POST /api/packages/{id}/course-rules/preview — the selection simulator and the headcount
    /// forecast (contract §13.3).
    /// </summary>
    [HttpPost("{id:int}/course-rules/preview")]
    [ProducesResponseType(typeof(PackageCourseRulePreviewDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> PreviewCourseRules(
        int id,
        [FromBody] PackageCourseRulePreviewRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _previewCourseRules.ExecuteAsync(id, request, cancellationToken);

        if (result.Succeeded)
        {
            return Ok(result.Data);
        }

        return result is PackageNotFoundResult<PackageCourseRulePreviewDto>
            ? NotFound(new { errors = result.Errors })
            : BadRequest(new { errors = result.Errors });
    }

    /// <summary>
    /// GET /api/packages/{id}/areas — the tier's coverage matrix (contract §13.4).
    /// </summary>
    [HttpGet("{id:int}/areas")]
    [ProducesResponseType(typeof(PackageAreaCoverageMatrixDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAreaCoverage(int id, CancellationToken cancellationToken)
    {
        var result = await _getCoverageMatrix.ExecuteAsync(id, cancellationToken);

        return result.Succeeded
            ? Ok(result.Data)
            : NotFound(new { errors = result.Errors });
    }

    /// <summary>
    /// PUT /api/packages/{id}/areas — replace the tier's coverage set (contract §13.5).
    /// </summary>
    [HttpPut("{id:int}/areas")]
    [ProducesResponseType(typeof(PackageAreaCoverageMatrixDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ReplaceAreaCoverage(
        int id,
        [FromBody] PackageAreaCoverageRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _replaceCoverage.ExecuteAsync(id, request, cancellationToken);

        if (result.Succeeded)
        {
            return Ok(result.Data);
        }

        return result is PackageNotFoundResult<PackageAreaCoverageMatrixDto>
            ? NotFound(new { errors = result.Errors })
            : BadRequest(new { errors = result.Errors });
    }

    /// <summary>
    /// POST /api/packages/{id}/areas — attach a municipality to the tier (contract §13.6).
    /// </summary>
    [HttpPost("{id:int}/areas")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AttachAreaCoverage(
        int id,
        [FromBody] PackageAreaCoverageAttachDto request,
        CancellationToken cancellationToken)
    {
        var result = await _attachCoverage.ExecuteAsync(id, request, cancellationToken);

        if (result.Succeeded)
        {
            return Created(
                $"/api/packages/{id}/areas",
                new { packageAreaCoverageId = result.Data });
        }

        return result is PackageNotFoundResult<int>
            ? NotFound(new { errors = result.Errors })
            : BadRequest(new { errors = result.Errors });
    }

    /// <summary>
    /// POST /api/packages/{id}/areas/preview — the out-of-town pricing simulator (contract §13.7).
    /// </summary>
    [HttpPost("{id:int}/areas/preview")]
    [ProducesResponseType(typeof(PackageLocationPreviewDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> PreviewLocation(
        int id,
        [FromBody] PackageLocationPreviewRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _previewLocation.ExecuteAsync(id, request, cancellationToken);

        if (result.Succeeded)
        {
            return Ok(result.Data);
        }

        // A missing tier and a missing municipality are both 404s; a rejected payload is a 400.
        return result is PackageNotFoundResult<PackageLocationPreviewDto>
            || result is AreaNotFoundResult<PackageLocationPreviewDto>
                ? NotFound(new { errors = result.Errors })
                : BadRequest(new { errors = result.Errors });
    }

    /// <summary>
    /// GET /api/packages/{id}/spec-sheet — the tier's printable specification sheet
    /// (contract §13.8).
    /// </summary>
    [HttpGet("{id:int}/spec-sheet")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetSpecSheet(int id, CancellationToken cancellationToken)
    {
        var result = await _getSpecSheet.ExecuteAsync(id, cancellationToken);

        if (!result.Succeeded)
        {
            return NotFound(new { errors = result.Errors });
        }

        var document = new PackageSpecSheetDocument(result.Data!);
        var bytes = document.GeneratePdf();

        return File(bytes, "application/pdf", $"PackageSpecSheet-{id}-{DateTime.Now:yyyyMMdd}.pdf");
    }
}
