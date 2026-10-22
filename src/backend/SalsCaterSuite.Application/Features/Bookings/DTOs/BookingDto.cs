using SalsCaterSuite.Domain.Enums;

namespace SalsCaterSuite.Application.Features.Bookings.DTOs;

/// <summary>
/// Read-only projection of a booking used by the details and list endpoints.
/// Financial totals are server-calculated; the frontend must treat them as authoritative.
/// </summary>
public class BookingDto
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
    public decimal AmountDue { get; set; }
    public decimal DepositAmount { get; set; }
    public decimal AmountPaid { get; set; }
    public decimal Balance { get; set; }

    // Status
    public BookingStatus Status { get; set; } = BookingStatus.Pending;
    public bool ReminderSent { get; set; }
}

/// <summary>
/// Request body for POST /api/bookings.
/// <para>
/// The body was extended by REQ-BKG-05 (contract §14.4): the dispatch schedule, the discount
/// code, the deposit, the <c>confirm</c> switch and the optional initial payment were added to
/// the existing fields. Every extension is <b>additive</b> — a payload that omits them behaves
/// exactly as before.
/// </para>
/// </summary>
public class CreateBookingRequestDto
{
    public int CustomerId { get; set; }
    public BookingType BookType { get; set; } = BookingType.Inside;
    public ServiceType TypeOfService { get; set; } = ServiceType.Catering;
    public int NoOfPerson { get; set; }
    public string Occasion { get; set; } = string.Empty;
    public string Venue { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string? EventColor { get; set; }
    public int? PackageId { get; set; }
    public string? Reference { get; set; }
    public int? ExtendedAreaId { get; set; }
    public bool ApplyExtendedAmount { get; set; }
    public string? SpecialInstructions { get; set; }
    /// <summary>
    /// Optional override for the package price per head. When null the system
    /// reads AmountPax from the selected Package record at the time of creation.
    /// </summary>
    public decimal? OverridePricePerHead { get; set; }

    // ─────────────── REQ-BKG-05 extensions (contract §14.4) ───────────────

    /// <summary>The kitchen call time (DEC-106).</summary>
    public TimeSpan? CallTime { get; set; }

    /// <summary>The pack-out time (DEC-106).</summary>
    public TimeSpan? PackoutTime { get; set; }

    /// <summary>The discount code to resolve against the discount catalogue (BR-02).</summary>
    public string? DiscountCode { get; set; }

    /// <summary>
    /// The deposit the operator agreed (BR-30). Advisory — it is never a second payment and it
    /// never changes the balance.
    /// </summary>
    public decimal? DepositAmount { get; set; }

    /// <summary>
    /// <c>false</c> creates a <b>draft</b> in the <c>Pending</c> status; <c>true</c> creates a
    /// <c>Confirmed</c> booking (BR-33, DEC-107). No draft entity and no new lifecycle status
    /// are introduced.
    /// </summary>
    public bool Confirm { get; set; }

    /// <summary>
    /// The initial payment, recorded in the <b>same commit</b> as the booking. Only a
    /// <b>confirmed</b> booking may carry one.
    /// </summary>
    public BookingInitialPaymentRequestDto? InitialPayment { get; set; }
}

/// <summary>
/// The initial payment of a confirmed booking (contract §14.4).
/// </summary>
public class BookingInitialPaymentRequestDto
{
    /// <summary>The amount collected. It must not exceed the net amount due (BR-05).</summary>
    public decimal AmountPaid { get; set; }

    /// <summary>The means of payment — the <c>PaymentMeans</c> <b>enum name</b> (contract §8).</summary>
    public PaymentMeans PaymentMeans { get; set; } = PaymentMeans.Cash;

    /// <summary>The cheque number, when the means is a cheque.</summary>
    public string? CheckNo { get; set; }

    /// <summary>Free-text notes.</summary>
    public string? Notes { get; set; }

    /// <summary>The description of the payment — e.g. "Downpayment".</summary>
    public string? Particular { get; set; }
}

