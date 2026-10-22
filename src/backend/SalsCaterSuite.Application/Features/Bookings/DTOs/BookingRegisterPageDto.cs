namespace SalsCaterSuite.Application.Features.Bookings.DTOs;

using SalsCaterSuite.Domain.Enums;

/// <summary>
/// The Bookings &amp; Events Register page (contract §14.1).
/// <para>
/// Every figure is server-computed (DEC-002): the client formats and displays, it never
/// derives a money figure, a percentage or a count.
/// </para>
/// </summary>
public class BookingRegisterPageDto
{
    /// <summary>The page's rows.</summary>
    public List<BookingRegisterItemDto> Items { get; set; } = new();

    /// <summary>The size of the <b>filtered population</b> — not the page.</summary>
    public int TotalCount { get; set; }

    /// <summary>The 1-based page number.</summary>
    public int Page { get; set; }

    /// <summary>The page size.</summary>
    public int PageSize { get; set; }

    /// <summary>The number of pages over the filtered population.</summary>
    public int TotalPages { get; set; }

    /// <summary>The totals strip — over the whole filtered population.</summary>
    public BookingRegisterTotalsDto Summary { get; set; } = new();

    /// <summary>The facet counts — each facet with its own dimension removed.</summary>
    public BookingRegisterFacetsDto QuickViewCounts { get; set; } = new();

    /// <summary>When the page was produced.</summary>
    public DateTime GeneratedAt { get; set; }
}

/// <summary>
/// The register's unpaged result set — the whole filtered population and its totals.
/// <para>
/// The XLSX export carries the very same filters as the register, but the <b>whole</b>
/// population and never the current page (contract §14.6).
/// </para>
/// </summary>
public class BookingRegisterExportData
{
    /// <summary>Every row of the filtered population, in the register's own order.</summary>
    public List<BookingRegisterItemDto> Items { get; set; } = new();

    /// <summary>The totals over the whole filtered population.</summary>
    public BookingRegisterTotalsDto Totals { get; set; } = new();
}

/// <summary>
/// The register's totals strip. Every figure is over the <b>whole filtered population</b>,
/// never over the page (BR-32).
/// </summary>
public class BookingRegisterTotalsDto
{
    /// <summary>The number of bookings in the filtered population.</summary>
    public int BookingCount { get; set; }

    /// <summary>The sum of the net amount due — the net contracted value (KPI-35).</summary>
    public decimal NetContractedValue { get; set; }

    /// <summary>The sum of the collected amounts (KPI-35).</summary>
    public decimal CollectedAmount { get; set; }

    /// <summary>The sum of the outstanding balances (KPI-35, KPI-39).</summary>
    public decimal OutstandingAmount { get; set; }

    /// <summary>The sum of the headcounts (KPI-36).</summary>
    public int TotalPax { get; set; }
}

/// <summary>
/// The register's facet counts (BR-32). Each facet is computed with <b>its own dimension
/// removed</b>, so every facet answers "how many would I get if I chose this?".
/// </summary>
public class BookingRegisterFacetsDto
{
    /// <summary>The tier-type counts.</summary>
    public List<BookingRegisterFacetCountDto> PackageTypes { get; set; } = new();

    /// <summary>The venue counts.</summary>
    public List<BookingRegisterFacetCountDto> Venues { get; set; } = new();

    /// <summary>The lifecycle-status counts.</summary>
    public List<BookingRegisterFacetCountDto> Statuses { get; set; } = new();

    /// <summary>The payment-state counts.</summary>
    public List<BookingRegisterFacetCountDto> PaymentStates { get; set; } = new();
}

/// <summary>One facet entry — a key and the number of bookings behind it.</summary>
public class BookingRegisterFacetCountDto
{
    /// <summary>The facet key — a tier type, a venue, a status name or a payment-state name.</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>How many bookings carry the key.</summary>
    public int Count { get; set; }
}

/// <summary>
/// One dossier card of the register (contract §14.1).
/// <para>
/// <c>netAmountDue = amountPaid + outstandingBalance</c> holds for every card, because the net
/// amount due comes from the one calculator and the collected amount from the authoritative
/// payment sum.
/// </para>
/// </summary>
public class BookingRegisterItemDto
{
    // ── Identity and schedule ───────────────────────────────────────────────────
    public int TransactionId { get; set; }
    public string BookingNumber { get; set; } = string.Empty;
    public DateOnly EventDate { get; set; }
    public TimeSpan EventStartTime { get; set; }
    public TimeSpan EventEndTime { get; set; }
    public TimeSpan? CallTime { get; set; }
    public TimeSpan? PackoutTime { get; set; }

    // ── Lifecycle and payment state (two separate vocabularies — DEC-117) ────────
    public BookingStatus Status { get; set; }
    public string PaymentState { get; set; } = string.Empty;
    public bool IsOverdue { get; set; }

    /// <summary>The days until the event, or <c>null</c> when the event is not in the future.</summary>
    public int? DaysUntilEvent { get; set; }

    // ── Client ──────────────────────────────────────────────────────────────────
    public int CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string? CustomerCompany { get; set; }
    public string? CustomerContact { get; set; }
    public string? Occasion { get; set; }
    public string? Venue { get; set; }

    // ── Tier ────────────────────────────────────────────────────────────────────
    public int? PackageId { get; set; }
    public string? PackageName { get; set; }
    public string? PackageType { get; set; }
    public int NumberOfHeads { get; set; }
    public decimal PricePerHead { get; set; }
    public decimal PackageAmount { get; set; }

    // ── Money (server-computed, DEC-002) ────────────────────────────────────────
    public decimal NetAmountDue { get; set; }
    public decimal AmountPaid { get; set; }
    public decimal OutstandingBalance { get; set; }

    // ── The frozen surcharge, the deposit and the discount ──────────────────────
    public int? ExtendedAreaId { get; set; }
    public string? ExtendedAreaName { get; set; }
    public decimal ExtendedAreaAmount { get; set; }
    public bool ApplyExtendedAmount { get; set; }
    public decimal DepositAmount { get; set; }
    public string? DiscountCode { get; set; }
    public decimal DiscountAmount { get; set; }

    /// <summary>
    /// The tier's <b>planning estimate</b> of the base gross profit percentage (DEC-103).
    /// <c>null</c> when the tier has no costed pool. It is never the event's own margin and it
    /// never enters a booking total.
    /// </summary>
    public decimal? BaseGrossProfitPercentage { get; set; }
}
