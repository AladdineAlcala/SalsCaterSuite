using SalsCaterSuite.Domain.Common;

namespace SalsCaterSuite.Domain.Entities;

public class Menu : BaseEntity
{
    public string MenuId { get; set; } = string.Empty;
    public int? CourseId { get; set; }
    public string MenuName { get; set; } = string.Empty;
    public int? DeptId { get; set; }
    public string? Note { get; set; }
    public decimal? Price { get; set; }
    public decimal? FoodCost { get; set; } = 0.00m;
    public string? Image { get; set; }
    public DateTime? DateAdded { get; set; } = DateTime.UtcNow;
    public DateTime? DateUpdated { get; set; }

    public CourseCategory? CourseCategory { get; set; }

    /// <summary>
    /// The department that owns the dish. The <c>deptId</c> column pre-dates the department
    /// master and was orphaned until the 2026-10-24 request wired it to <see cref="Department"/>.
    /// </summary>
    public Department? Department { get; set; }
}

