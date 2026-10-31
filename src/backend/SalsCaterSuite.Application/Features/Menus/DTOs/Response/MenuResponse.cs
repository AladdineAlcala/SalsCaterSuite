namespace SalsCaterSuite.Application.Features.Menus.DTOs.Response;

/// <summary>
/// The legacy menu projection (<c>GET /api/menus</c>, <c>GET /api/menus/{id}</c>).
/// <para>
/// It used to publish a <c>ProfitMargin</c> computed property. That property was
/// <b>removed</b> (RSK-030): a get-only computed property cannot be projected by EF Core, so
/// it always serialised as <c>0</c> — a field that always reports zero is worse than no
/// field. The real figures are published by the Menu &amp; Food Costing Master
/// (<c>GET /api/menus/master</c>) and by the costing endpoint
/// (<c>GET /api/menus/{id}/costing</c>).
/// </para>
/// </summary>
public class MenuResponse
{
    public string MenuId { get; set; } = string.Empty;
    public int? CourseId { get; set; }
    public string? CourseName { get; set; }
    public string MenuName { get; set; } = string.Empty;
    public string? Note { get; set; }
    public decimal? Price { get; set; }
    public decimal? FoodCost { get; set; }
    public string? Image { get; set; }
}
