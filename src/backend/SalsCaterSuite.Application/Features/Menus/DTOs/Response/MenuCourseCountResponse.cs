namespace SalsCaterSuite.Application.Features.Menus.DTOs.Response;

/// <summary>
/// One course tab (spec §4.3). The dish count is the count of the tab strip's scope — the
/// search and the status filters apply, the course filter does not — so the tab counts narrow
/// with the search and stay stable while a course is selected.
/// </summary>
public class MenuCourseCountResponse
{
    /// <summary>The course id.</summary>
    public int CourseId { get; set; }

    /// <summary>The course name.</summary>
    public string CourseName { get; set; } = string.Empty;

    /// <summary>The number of dishes in the tab strip's scope that belong to this course.</summary>
    public int DishCount { get; set; }
}
