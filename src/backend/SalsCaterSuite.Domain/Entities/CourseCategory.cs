namespace SalsCaterSuite.Domain.Entities;

public class CourseCategory
{
    public int CourseId { get; set; }
    public string CourseName { get; set; } = string.Empty;

    public ICollection<Menu> Menus { get; set; } = new List<Menu>();
}

