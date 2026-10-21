using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SalsCaterSuite.Application.Features.Menus;
using SalsCaterSuite.Application.Features.Menus.Commands;
using SalsCaterSuite.Application.Features.Menus.DTOs;
using SalsCaterSuite.Application.Features.Menus.Queries;
using SalsCaterSuite.Application.Features.Menus.Services;
using SalsCaterSuite.Infrastructure.Exports;

namespace SalsCaterSuite.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class MenusController : ControllerBase
{
    private readonly GetMenusQuery _getMenus;
    private readonly GetCoursesQuery _getCourses;
    private readonly GetMenuByIdQuery _getMenuById;
    private readonly GetMenuMasterQuery _getMenuMaster;
    private readonly PreviewMenuCostingQuery _previewCosting;
    private readonly CreateMenuCommand _createMenu;
    private readonly UpdateMenuCommand _updateMenu;
    private readonly DeleteMenuCommand _deleteMenu;
    private readonly IMenuCostingService _costingService;
    private readonly MenuFoodCostingExport _foodCostingExport;

    public MenusController(
        GetMenusQuery getMenus,
        GetCoursesQuery getCourses,
        GetMenuByIdQuery getMenuById,
        GetMenuMasterQuery getMenuMaster,
        PreviewMenuCostingQuery previewCosting,
        CreateMenuCommand createMenu,
        UpdateMenuCommand updateMenu,
        DeleteMenuCommand deleteMenu,
        IMenuCostingService costingService,
        MenuFoodCostingExport foodCostingExport)
    {
        _getMenus = getMenus;
        _getCourses = getCourses;
        _getMenuById = getMenuById;
        _getMenuMaster = getMenuMaster;
        _previewCosting = previewCosting;
        _createMenu = createMenu;
        _updateMenu = updateMenu;
        _deleteMenu = deleteMenu;
        _costingService = costingService;
        _foodCostingExport = foodCostingExport;
    }

    /// <summary>GET /api/menus — List all menu items</summary>
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] int? courseId, CancellationToken cancellationToken)
    {
        var result = await _getMenus.ExecuteAsync(courseId, cancellationToken);
        return Ok(result.Data);
    }

    /// <summary>GET /api/menus/courses — List all course categories</summary>
    [HttpGet("courses")]
    public async Task<IActionResult> GetCourses(CancellationToken cancellationToken)
    {
        var result = await _getCourses.ExecuteAsync(cancellationToken);
        return Ok(result.Data);
    }

    /// <summary>
    /// GET /api/menus/master — the paged Menu &amp; Food Costing Master (spec §6.1).
    /// </summary>
    /// <param name="search">Case-insensitive substring over the dish code, the dish name and the note.</param>
    /// <param name="courseId">Restricts the result set to one course; ignored when absent.</param>
    /// <param name="status"><c>Costed</c>, <c>Uncosted</c> or <c>MarginAlert</c>; any other value is ignored.</param>
    /// <param name="sort">
    /// <c>FoodCostAsc</c> (default), <c>FoodCostDesc</c>, <c>PriceDesc</c>, <c>PriceAsc</c>,
    /// <c>MarginDesc</c> or <c>NameAsc</c>; any other value falls back to the default.
    /// </param>
    /// <param name="page">The 1-based page number; values below 1 are clamped to 1.</param>
    /// <param name="pageSize">The page size; clamped to 1…100.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    [HttpGet("master")]
    [ProducesResponseType(typeof(MenuMasterDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMaster(
        [FromQuery] string? search,
        [FromQuery] int? courseId,
        [FromQuery] string? status,
        [FromQuery] string? sort,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = GetMenuMasterQuery.DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        var result = await _getMenuMaster.ExecuteAsync(
            search, courseId, status, sort, page, pageSize, cancellationToken);

        return Ok(result.Data);
    }

    /// <summary>
    /// GET /api/menus/master/export — the current filtered result set as XLSX (spec §6.3).
    /// The export is never paged.
    /// </summary>
    [HttpGet("master/export")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ExportMaster(
        [FromQuery] string? search,
        [FromQuery] int? courseId,
        [FromQuery] string? status,
        [FromQuery] string? sort,
        CancellationToken cancellationToken)
    {
        var result = await _getMenuMaster.ExecuteUnpagedAsync(
            search, courseId, status, sort, cancellationToken);

        var bytes = _foodCostingExport.Generate(result.Data!);

        return File(
            bytes,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"MenuFoodCosting-{DateTime.Now:yyyyMMdd}.xlsx");
    }

    /// <summary>
    /// POST /api/menus/costing/preview — the server-computed margin preview (spec §6.2).
    /// <para>
    /// The route is a literal two-segment path, so it can never be captured by
    /// <c>GET /api/menus/{id}</c> or by <c>GET /api/menus/{id}/costing</c>.
    /// </para>
    /// </summary>
    [HttpPost("costing/preview")]
    [ProducesResponseType(typeof(MenuCostingPreviewDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> PreviewCosting(
        [FromBody] MenuCostingPreviewRequestDto request,
        CancellationToken cancellationToken)
    {
        var result = await _previewCosting.ExecuteAsync(request, cancellationToken);

        return result.Succeeded
            ? Ok(result.Data)
            : BadRequest(new { errors = result.Errors });
    }

    /// <summary>GET /api/menus/{id} — Get menu item by ID</summary>
    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(string id, CancellationToken cancellationToken)
    {
        var result = await _getMenuById.ExecuteAsync(id, cancellationToken);
        return result.Succeeded ? Ok(result.Data) : NotFound();
    }

    /// <summary>GET /api/menus/{id}/costing — Get menu costing analysis</summary>
    [HttpGet("{id}/costing")]
    public async Task<IActionResult> GetCosting(string id, CancellationToken cancellationToken)
    {
        var result = await _costingService.CalculateCostingAsync(id, cancellationToken);
        return result.Succeeded ? Ok(result.Data) : NotFound(new { errors = result.Errors });
    }

    /// <summary>GET /api/menus/costing/all — Get all menu costings</summary>
    [HttpGet("costing/all")]
    public async Task<IActionResult> GetAllCostings(CancellationToken cancellationToken)
    {
        var result = await _costingService.GetAllCostingsAsync(cancellationToken);
        return result.Succeeded ? Ok(result.Data) : BadRequest(new { errors = result.Errors });
    }

    /// <summary>POST /api/menus — Create new menu item</summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateMenuRequestDto request, CancellationToken cancellationToken)
    {
        var result = await _createMenu.ExecuteAsync(request, cancellationToken);
        if (!result.Succeeded)
        {
            // A rejected payload is a validation failure (400); a taken dish code is a conflict
            // (409). The two are distinguished by the result type, never by the message text.
            // Both use the uniform { "errors": [ ... ] } envelope (contract §7, spec §7.6).
            return result is MenuDuplicateResult
                ? Conflict(new { errors = result.Errors })
                : BadRequest(new { errors = result.Errors });
        }

        return CreatedAtAction(nameof(GetById), new { id = result.Data }, new { menuId = result.Data });
    }

    /// <summary>PUT /api/menus/{id} — Update menu item</summary>
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(string id, [FromBody] CreateMenuRequestDto request, CancellationToken cancellationToken)
    {
        var result = await _updateMenu.ExecuteAsync(id, request, cancellationToken);
        if (result.Succeeded)
        {
            return NoContent();
        }

        // A rejected payload is a validation failure (400); a missing dish is a 404.
        return result is MenuNotFoundResult
            ? NotFound(new { errors = result.Errors })
            : BadRequest(new { errors = result.Errors });
    }

    /// <summary>DELETE /api/menus/{id} — Delete menu item</summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id, CancellationToken cancellationToken)
    {
        var result = await _deleteMenu.ExecuteAsync(id, cancellationToken);
        return result.Succeeded ? NoContent() : NotFound();
    }
}
