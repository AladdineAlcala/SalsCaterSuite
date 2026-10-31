namespace SalsCaterSuite.Application.Features.Menus.DTOs.Request;

/// <summary>
/// The body of the menu write commands (spec §6.4, §7.7).
/// </summary>
public class CreateMenuRequest
{
    public string MenuId { get; set; } = string.Empty;
    public int CourseId { get; set; }
    public string MenuName { get; set; } = string.Empty;
    public string? Note { get; set; }
    public decimal Price { get; set; }
    public decimal FoodCost { get; set; }

    /// <summary>
    /// The plating photo — a URL string. Optional; the column is <c>nvarchar(250)</c>
    /// (spec §5.2, §7.7).
    /// </summary>
    public string? Image { get; set; }
}
