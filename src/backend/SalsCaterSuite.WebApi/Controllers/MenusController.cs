using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SalsCaterSuite.Application.Common.Interfaces;
using SalsCaterSuite.Application.Features.Menus.DTOs;
using SalsCaterSuite.Application.Features.Menus.Services;
using SalsCaterSuite.Domain.Entities;

namespace SalsCaterSuite.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class MenusController : ControllerBase
{
    private readonly ISalsCaterSuiteDbContext _context;
    private readonly IMenuCostingService _costingService;

    public MenusController(ISalsCaterSuiteDbContext context, IMenuCostingService costingService)
    {
        _context = context;
        _costingService = costingService;
    }

    /// <summary>GET /api/menus — List all menu items</summary>
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] int? courseId, CancellationToken cancellationToken)
    {
        var query = _context.Menus.AsNoTracking()
            .Include(m => m.CourseCategory);

        if (courseId.HasValue)
            query = (Microsoft.EntityFrameworkCore.Query.IIncludableQueryable<Menu, CourseCategory?>)
                query.Where(m => m.CourseId == courseId.Value);

        var menus = await query
            .OrderBy(m => m.CourseId).ThenBy(m => m.MenuName)
            .Select(m => new MenuDto
            {
                MenuId = m.MenuId,
                CourseId = m.CourseId,
                CourseName = m.CourseCategory != null ? m.CourseCategory.CourseName : null,
                MenuName = m.MenuName ?? string.Empty,
                Note = m.Note,
                Price = m.Price,
                FoodCost = m.FoodCost
            })
            .ToListAsync(cancellationToken);

        return Ok(menus);
    }

    /// <summary>GET /api/menus/courses — List all course categories</summary>
    [HttpGet("courses")]
    public async Task<IActionResult> GetCourses(CancellationToken cancellationToken)
    {
        var courses = await _context.CourseCategories
            .AsNoTracking()
            .OrderBy(c => c.CourseId)
            .Select(c => new { c.CourseId, c.CourseName })
            .ToListAsync(cancellationToken);

        return Ok(courses);
    }

    /// <summary>GET /api/menus/{id} — Get menu item by ID</summary>
    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(string id, CancellationToken cancellationToken)
    {
        var menu = await _context.Menus
            .AsNoTracking()
            .Include(m => m.CourseCategory)
            .Where(m => m.MenuId == id)
            .Select(m => new MenuDto
            {
                MenuId = m.MenuId,
                CourseId = m.CourseId,
                CourseName = m.CourseCategory != null ? m.CourseCategory.CourseName : null,
                MenuName = m.MenuName ?? string.Empty,
                Note = m.Note,
                Price = m.Price,
                FoodCost = m.FoodCost
            })
            .FirstOrDefaultAsync(cancellationToken);

        return menu is null ? NotFound() : Ok(menu);
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
        var exists = await _context.Menus.AnyAsync(m => m.MenuId == request.MenuId, cancellationToken);
        if (exists)
            return Conflict(new { message = $"Menu item '{request.MenuId}' already exists." });

        var menu = new Menu
        {
            MenuId = request.MenuId.ToUpper(),
            CourseId = request.CourseId,
            MenuName = request.MenuName,
            Note = request.Note,
            Price = request.Price,
            FoodCost = request.FoodCost
        };

        _context.Menus.Add(menu);
        await _context.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(nameof(GetById), new { id = menu.MenuId }, new { menuId = menu.MenuId });
    }

    /// <summary>PUT /api/menus/{id} — Update menu item</summary>
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(string id, [FromBody] CreateMenuRequestDto request, CancellationToken cancellationToken)
    {
        var menu = await _context.Menus.FirstOrDefaultAsync(m => m.MenuId == id, cancellationToken);
        if (menu is null) return NotFound();

        menu.CourseId = request.CourseId;
        menu.MenuName = request.MenuName;
        menu.Note = request.Note;
        menu.Price = request.Price;
        menu.FoodCost = request.FoodCost;

        await _context.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    /// <summary>DELETE /api/menus/{id} — Delete menu item</summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id, CancellationToken cancellationToken)
    {
        var menu = await _context.Menus.FirstOrDefaultAsync(m => m.MenuId == id, cancellationToken);
        if (menu is null) return NotFound();

        _context.Menus.Remove(menu);
        await _context.SaveChangesAsync(cancellationToken);
        return NoContent();
    }
}

