using SalsCaterSuite.Domain.Common;

namespace SalsCaterSuite.Domain.Entities;

public class CourseCategory : BaseEntity
{
    public int CourseId { get; set; }
    public string CourseName { get; set; } = string.Empty;

    public ICollection<Menu> Menus { get; set; } = new List<Menu>();
}

