using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SalsCaterSuite.Application.Features.Menus.Commands;
using SalsCaterSuite.Application.Features.Menus.DTOs;
using SalsCaterSuite.Application.Features.Menus.Queries;
using SalsCaterSuite.Application.Features.Menus.Services;

namespace SalsCaterSuite.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class MenusController : ControllerBase
{
    private readonly GetMenusQuery _getMenus;
    private readonly GetCoursesQuery _getCourses;
    private readonly GetMenuByIdQuery _getMenuById;
    private readonly CreateMenuCommand _createMenu;
    private readonly UpdateMenuCommand _updateMenu;
    private readonly DeleteMenuCommand _deleteMenu;
    private readonly IMenuCostingService _costingService;

    public MenusController(
        GetMenusQuery getMenus,
        GetCoursesQuery getCourses,
        GetMenuByIdQuery getMenuById,
        CreateMenuCommand createMenu,
        UpdateMenuCommand updateMenu,
        DeleteMenuCommand deleteMenu,
        IMenuCostingService costingService)
    {
        _getMenus = getMenus;
        _getCourses = getCourses;
        _getMenuById = getMenuById;
        _createMenu = createMenu;
        _updateMenu = updateMenu;
        _deleteMenu = deleteMenu;
        _costingService = costingService;
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
            return Conflict(new { message = result.Errors[0] });
        }

        return CreatedAtAction(nameof(GetById), new { id = result.Data }, new { menuId = result.Data });
    }

    /// <summary>PUT /api/menus/{id} — Update menu item</summary>
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(string id, [FromBody] CreateMenuRequestDto request, CancellationToken cancellationToken)
    {
        var result = await _updateMenu.ExecuteAsync(id, request, cancellationToken);
        return result.Succeeded ? NoContent() : NotFound();
    }

    /// <summary>DELETE /api/menus/{id} — Delete menu item</summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id, CancellationToken cancellationToken)
    {
        var result = await _deleteMenu.ExecuteAsync(id, cancellationToken);
        return result.Succeeded ? NoContent() : NotFound();
    }
}
