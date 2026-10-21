namespace SalsCaterSuite.Application.Features.Customers.DTOs;

/// <summary>
/// The paged customer directory payload (spec §5.1).
/// <para>
/// Every figure is computed server-side (DEC-002). <see cref="Summary"/> describes the
/// <b>whole filtered result set</b>, not the current page.
/// </para>
/// </summary>
public class CustomerDirectoryDto
{
    /// <summary>The KPI cards of the directory — computed over the whole filtered set.</summary>
    public CustomerDirectorySummaryDto Summary { get; set; } = new();

    /// <summary>
    /// The quick-view pills' live counts (spec §4.3). They describe the
    /// <b>search-filtered population with the type and AR-state filters ignored</b>, so the
    /// pills always report the population the operator is looking at — they are a
    /// navigation aid, not a readout of the current filter.
    /// </summary>
    public CustomerQuickViewCountsDto QuickViewCounts { get; set; } = new();

    /// <summary>The rows of the requested page.</summary>
    public List<CustomerDirectoryItemDto> Items { get; set; } = new();

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

/// <summary>
/// The quick-view pills' live counts (spec §4.3, contract §10.1).
/// <para>
/// Every count describes the <b>search-filtered population with the <c>customerType</c> and
/// <c>arState</c> filters ignored</c>. <c>Corporate + Private = All</c> always holds.
/// </para>
/// </summary>
public class CustomerQuickViewCountsDto
{
    /// <summary>Non-deleted customers matching the search filter.</summary>
    public int All { get; set; }

    /// <summary>Of that population, those whose company name is not blank (DEC-051).</summary>
    public int Corporate { get; set; }

    /// <summary>Of that population, those whose company name is blank (DEC-051).</summary>
    public int Private { get; set; }

    /// <summary>Of that population, those with a positive outstanding balance (KPI-12).</summary>
    public int WithOpenBalance { get; set; }

    /// <summary>
    /// Of that population, those whose worst aging bucket is <c>Days31To60</c>,
    /// <c>Days61To90</c> or <c>Over90Days</c> (KPI-13).
    /// </summary>
    public int Overdue30 { get; set; }
}

/// <summary>
/// The directory's KPI cards (spec §4.2).
/// </summary>
public class CustomerDirectorySummaryDto
{
    /// <summary>KPI-20 — the number of registered (non-deleted) customers in the filtered set.</summary>
    public int TotalClients { get; set; }

    /// <summary>
    /// KPI-12 — <c>Σ max(0, NetAmountDue − AmountPaid)</c> over the filtered set's
    /// non-cancelled bookings (DEC-025, BR-03).
    /// </summary>
    public decimal ActiveArAmount { get; set; }

    /// <summary>The number of filtered customers that carry a positive balance.</summary>
    public int ClientsWithBalance { get; set; }

    /// <summary>KPI-21 — <c>Σ NetAmountDue</c> over the filtered set's non-cancelled bookings.</summary>
    public decimal TotalContractedValue { get; set; }

    /// <summary>
    /// <c>TotalContractedValue ÷ TotalClients</c>, or <c>null</c> when there are no clients.
    /// A <c>null</c> means "undefined" — never <c>0</c> (contract §9.2 rule 6, "no null-as-zero").
    /// </summary>
    public decimal? AverageValuePerClient { get; set; }

    /// <summary>
    /// KPI-13 — the outstanding balance of the filtered customers whose aging state is more
    /// than 30 days overdue.
    /// </summary>
    public decimal OverdueAmount { get; set; }

    /// <summary>The number of filtered customers whose aging state is more than 30 days overdue.</summary>
    public int OverdueClientCount { get; set; }
}

/// <summary>
/// One row of the customer directory (spec §4.5).
/// </summary>
public class CustomerDirectoryItemDto
{
    public int CustomerId { get; set; }

    public string LastName { get; set; } = string.Empty;

    public string FirstName { get; set; } = string.Empty;

    public string? Middle { get; set; }

    public string FullName { get; set; } = string.Empty;

    public string? Company { get; set; }

    public string? Address { get; set; }

    public string? Contact1 { get; set; }

    public string? Contact2 { get; set; }

    public DateTime? DateRegistered { get; set; }

    /// <summary>
    /// The derived account classification: <c>Corporate</c> when the company name is not blank,
    /// <c>Private</c> otherwise (spec §4.4, DEC-051). It is a presentation of an existing
    /// attribute and is never persisted.
    /// </summary>
    public string CustomerType { get; set; } = string.Empty;

    /// <summary>The customer's booking counters.</summary>
    public CustomerBookingCountsDto BookingCounts { get; set; } = new();

    /// <summary>KPI-21 — <c>Σ NetAmountDue</c> over the customer's non-cancelled bookings.</summary>
    public decimal TotalContractedValue { get; set; }

    /// <summary><c>Σ Payment.AmountPaid</c> over the customer's non-cancelled bookings (DEC-012).</summary>
    public decimal TotalPaid { get; set; }

    /// <summary>
    /// <c>Σ max(0, NetAmountDue − AmountPaid)</c> over the customer's non-cancelled bookings
    /// (DEC-025, BR-03).
    /// </summary>
    public decimal OutstandingBalance { get; set; }

    /// <summary>The aging bucket key, e.g. <c>Clean</c> or <c>Days31To60</c>.</summary>
    public string LedgerState { get; set; } = string.Empty;

    /// <summary>The server-provided display label of <see cref="LedgerState"/>.</summary>
    public string LedgerStateLabel { get; set; } = string.Empty;
}

/// <summary>
/// A customer's booking counters (spec §4.5).
/// </summary>
public class CustomerBookingCountsDto
{
    /// <summary>The bookings that are not cancelled.</summary>
    public int Total { get; set; }

    /// <summary>The non-cancelled bookings whose event date is in the past.</summary>
    public int Past { get; set; }

    /// <summary>The non-cancelled bookings whose event date is today or later.</summary>
    public int Upcoming { get; set; }

    /// <summary>The bookings whose lifecycle status is <c>Completed</c>.</summary>
    public int Completed { get; set; }

    /// <summary>The bookings whose lifecycle status is <c>Cancelled</c>.</summary>
    public int Cancelled { get; set; }
}
