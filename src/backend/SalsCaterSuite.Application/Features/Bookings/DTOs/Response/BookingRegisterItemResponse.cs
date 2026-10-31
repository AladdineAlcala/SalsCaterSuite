using SalsCaterSuite.Domain.Enums;

namespace SalsCaterSuite.Application.Features.Bookings.DTOs.Response;

/// <summary>
/// One dossier card of the register (contract §14.1).
/// <para>
/// <c>netAmountDue = amountPaid + outstandingBalance</c> holds for every card, because the net
/// amount due comes from the one calculator and the collected amount from the authoritative
/// payment sum.
/// </para>
/// </summary>
public class BookingRegisterItemResponse
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

    // ── The derived discount source and the selection/add-on state (DEC-154, BR-28, BR-39) ──

    /// <summary>The <b>derived</b> source of the discount (BR-44, DEC-154).</summary>
    public DiscountSource DiscountSource { get; set; }

    /// <summary>Exactly one of <c>None</c>, <c>Selected</c> or <c>Locked</c> (BR-28, M2).</summary>
    public BookingSelectionState SelectionState { get; set; }

    /// <summary>The number of live add-on lines the booking carries.</summary>
    public int AddonCount { get; set; }

    /// <summary>The frozen add-on line total that joins the gross (DEC-135, DEC-151).</summary>
    public decimal AddonsTotal { get; set; }
}
