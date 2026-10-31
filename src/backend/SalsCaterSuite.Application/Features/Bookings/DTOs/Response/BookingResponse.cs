using SalsCaterSuite.Domain.Enums;

namespace SalsCaterSuite.Application.Features.Bookings.DTOs.Response;

/// <summary>
/// Read-only projection of a booking used by the details and list endpoints.
/// Financial totals are server-calculated; the frontend must treat them as authoritative.
/// </summary>
public class BookingResponse
{
    public int TransactionId { get; set; }
    public string BookingNumber { get; set; } = string.Empty;
    public DateTime TransactionDate { get; set; }
    public string BookType { get; set; } = string.Empty;       // enum name, e.g. "Inside"
    public string TypeOfService { get; set; } = string.Empty;  // enum name, e.g. "Catering"
    public int CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string? CustomerContact { get; set; }
    public string? Occasion { get; set; }
    public string? Venue { get; set; }
    public DateOnly EventDate { get; set; }
    public TimeSpan EventStartTime { get; set; }
    public TimeSpan EventEndTime { get; set; }

    /// <summary>
    /// The kitchen call time — the dispatch schedule (BR-34, DEC-106).
    /// <c>null</c> = not stated.
    /// </summary>
    public TimeSpan? CallTime { get; set; }

    /// <summary>
    /// The pack-out time — the dispatch schedule (BR-34, DEC-106).
    /// <c>null</c> = not stated.
    /// </summary>
    public TimeSpan? PackoutTime { get; set; }

    public int NumberOfHeads { get; set; }
    public string? EventColor { get; set; }
    public int? PackageId { get; set; }
    public string? PackageName { get; set; }
    public string? Reference { get; set; }

    // Price snapshot (frozen at creation)
    public decimal PricePerHead { get; set; }
    public decimal PackageAmount { get; set; }

    /// <summary>
    /// The <b>frozen</b> out-of-town surcharge in pesos (BR-26, DEC-116) — the amount the
    /// booking was created with, <b>never</b> the municipality's current rate. A later rate
    /// change moves the quoted price of every new booking and never a stored one (BR-04).
    /// </summary>
    public decimal ExtendedAreaAmount { get; set; }

    /// <summary>
    /// The per-guest rate the <b>frozen</b> surcharge was derived from — the frozen amount
    /// divided by the headcount, which recovers the municipality's rate (BR-17, BR-26) exactly,
    /// because a two-decimal rate times an integer headcount is exact at two decimals.
    /// <para>
    /// It is published so a reader of the printed contract can see <b>how</b> the surcharge was
    /// derived (rate × pax). It is <c>null</c> when no surcharge was applied — "not stated" is
    /// null, never zero (BR-20).
    /// </para>
    /// </summary>
    public decimal? ExtendedAreaRatePerGuest { get; set; }

    // Financial breakdown
    public decimal Subtotal { get; set; }
    public string DiscountType { get; set; } = "None";
    public decimal DiscountPercentage { get; set; }

    /// <summary>
    /// The <b>frozen</b> code of the applied discount (DEC-106, BR-02) — what makes the booking
    /// auditable. <c>null</c> = no discount was applied.
    /// </summary>
    public string? DiscountCode { get; set; }

    public decimal DiscountAmount { get; set; }
    public decimal TaxAmount { get; set; }

    /// <summary>
    /// The <b>frozen</b> add-on line total (DEC-151) — BR-01's third term, which joins the gross
    /// (DEC-135). Zero when the booking carries no add-ons.
    /// </summary>
    public decimal AddonsTotal { get; set; }

    public decimal AmountDue { get; set; }
    public decimal DepositAmount { get; set; }
    public decimal AmountPaid { get; set; }
    public decimal Balance { get; set; }

    // Status
    public BookingStatus Status { get; set; } = BookingStatus.Pending;
    public bool ReminderSent { get; set; }
}
