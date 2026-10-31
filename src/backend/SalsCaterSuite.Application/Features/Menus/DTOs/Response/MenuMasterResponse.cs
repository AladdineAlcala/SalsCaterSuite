namespace SalsCaterSuite.Application.Features.Menus.DTOs.Response;

/// <summary>
/// The Menu &amp; Food Costing Master payload (spec §6.1, REQ-MNU-01/REQ-MNU-02).
/// <para>
/// Every figure is computed <b>server-side</b> (DEC-002). The browser formats and displays; it
/// computes no margin, no percentage and no cost figure.
/// </para>
/// <para>
/// <see cref="Summary"/> describes the <b>whole filtered result set</b>, not the current page,
/// while <see cref="Courses"/> describes the <b>whole catalogue</b> (the course tab counts stay
/// stable while a scope is selected).
/// </para>
/// </summary>
public class MenuMasterResponse
{
    /// <summary>The four KPI cards of the master screen — computed over the whole filtered set.</summary>
    public MenuMasterSummaryResponse Summary { get; set; } = new();

    /// <summary>
    /// The course tab strip. Its scope is <b>everything except the course filter</b>: the
    /// dishes matching the search and the status filters, with no course filter. The tab
    /// counts therefore narrow with the search (the Customer Master's <c>quickViewCounts</c>
    /// pattern) and "All Dishes (n)" is exact (spec §6.1).
    /// </summary>
    public MenuCoursesResponse Courses { get; set; } = new();

    /// <summary>The rows of the requested page.</summary>
    public List<MenuMasterItemResponse> Items { get; set; } = new();

    /// <summary>The 1-based page that was returned.</summary>
    public int Page { get; set; }

    /// <summary>The page size that was applied.</summary>
    public int PageSize { get; set; }

    /// <summary>The number of dishes in the whole filtered set.</summary>
    public int TotalCount { get; set; }

    /// <summary><c>ceil(TotalCount ÷ PageSize)</c>.</summary>
    public int TotalPages { get; set; }

    /// <summary>The server UTC timestamp at which the aggregate was produced.</summary>
    public DateTime GeneratedAt { get; set; }
}
