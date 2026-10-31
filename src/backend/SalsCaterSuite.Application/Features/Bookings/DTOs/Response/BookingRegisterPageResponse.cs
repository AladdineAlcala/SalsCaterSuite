using SalsCaterSuite.Domain.Enums;

namespace SalsCaterSuite.Application.Features.Bookings.DTOs.Response;

/// <summary>
/// The Bookings &amp; Events Register page (contract §14.1).
/// <para>
/// Every figure is server-computed (DEC-002): the client formats and displays, it never
/// derives a money figure, a percentage or a count.
/// </para>
/// </summary>
public class BookingRegisterPageResponse
{
    /// <summary>The page's rows.</summary>
    public List<BookingRegisterItemResponse> Items { get; set; } = new();

    /// <summary>The size of the <b>filtered population</b> — not the page.</summary>
    public int TotalCount { get; set; }

    /// <summary>The 1-based page number.</summary>
    public int Page { get; set; }

    /// <summary>The page size.</summary>
    public int PageSize { get; set; }

    /// <summary>The number of pages over the filtered population.</summary>
    public int TotalPages { get; set; }

    /// <summary>The totals strip — over the whole filtered population.</summary>
    public BookingRegisterTotalsResponse Summary { get; set; } = new();

    /// <summary>The facet counts — each facet with its own dimension removed.</summary>
    public BookingRegisterFacetsResponse QuickViewCounts { get; set; } = new();

    /// <summary>When the page was produced.</summary>
    public DateTime GeneratedAt { get; set; }
}
