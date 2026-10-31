namespace SalsCaterSuite.Application.Features.Menus.DTOs.Response;

/// <summary>Lightweight course-category projection used by GET /api/menus/courses.</summary>
public class CourseResponse
{
    public int CourseId { get; set; }
    public string CourseName { get; set; } = string.Empty;
}
