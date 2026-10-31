namespace SalsCaterSuite.Application.Features.Customers.DTOs.Response;

/// <summary>
/// The paged customer directory payload (spec §5.1).
/// <para>
/// Every figure is computed server-side (DEC-002). <see cref="Summary"/> describes the
/// <b>whole filtered result set</b>, not the current page.
/// </para>
/// </summary>
public class CustomerDirectoryResponse
{
    /// <summary>The KPI cards of the directory — computed over the whole filtered set.</summary>
    public CustomerDirectorySummaryResponse Summary { get; set; } = new();

    /// <summary>
    /// The quick-view pills' live counts (spec §4.3). They describe the
    /// <b>search-filtered population with the type and AR-state filters ignored</b>, so the
    /// pills always report the population the operator is looking at — they are a
    /// navigation aid, not a readout of the current filter.
    /// </summary>
    public CustomerQuickViewCountsResponse QuickViewCounts { get; set; } = new();

    /// <summary>The rows of the requested page.</summary>
    public List<CustomerDirectoryItemResponse> Items { get; set; } = new();

    /// <summary>The 1-based page that was returned.</summary>
    public int Page { get; set; }

    /// <summary>The page size that was applied.</summary>
    public int PageSize { get; set; }

    /// <summary>The number of rows in the whole filtered set.</summary>
    public int TotalCount { get; set; }

    /// <summary><c>ceil(TotalCount ÷ PageSize)</c>.</summary>
    public int TotalPages { get; set; }

    /// <summary>
    /// The server UTC timestamp at which the aggregate was produced, exactly as
    /// <c>GET /api/dashboard</c> reports it. The client never renders a client clock as the
    /// data's age (spec §4.10).
    /// </summary>
    public DateTime GeneratedAt { get; set; }
}
