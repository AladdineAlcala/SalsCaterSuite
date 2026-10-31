namespace SalsCaterSuite.Application.Features.Menus.DTOs.Response;

/// <summary>
/// The course tab strip (spec §4.3, §6.1).
/// <para>
/// Its scope is <b>everything except the course filter</b> — the dishes matching the search
/// and the status filters. The tab counts therefore narrow with the search, and
/// <see cref="Total"/> is the exact "All Dishes (n)" count of the current scope.
/// </para>
/// </summary>
public class MenuCoursesResponse
{
    /// <summary>
    /// The number of dishes in the tab strip's scope — the "All Dishes (n)" count. A dish
    /// without a course is included here even though it has no tab of its own, so
    /// <c>Σ items[].dishCount</c> may be smaller than this number.
    /// </summary>
    public int Total { get; set; }

    /// <summary>One entry per course category, with its live dish count.</summary>
    public List<MenuCourseCountResponse> Items { get; set; } = new();
}
