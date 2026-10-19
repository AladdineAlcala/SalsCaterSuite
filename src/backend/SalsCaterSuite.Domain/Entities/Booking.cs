using System.Globalization;
using SalsCaterSuite.Domain.Common;
using SalsCaterSuite.Domain.Enums;
using SalsCaterSuite.Domain.Exceptions;

namespace SalsCaterSuite.Domain.Entities;

/// <summary>
/// A confirmed catering booking. Financial fields (PricePerHead, PackageName, …)
/// are frozen at creation time so that later changes to the Package catalogue
/// never retroactively alter a booking's economics.
/// </summary>
public class Booking : BaseEntity
{
    // ─────────────── Surrogate PK (maps to legacy trn_Id) ───────────────
    public int TransactionId { get; private set; }

    // ─────────────── Identity ───────────────
    public string BookingNumber { get; private set; } = string.Empty;

    // ─────────────── Classification ───────────────
    public DateTime TransactionDate { get; set; } = DateTime.UtcNow;
    public BookingType BookType { get; set; }
    public ServiceType TypeOfService { get; set; }
    public string? Occasion { get; set; }
    public string? EventColor { get; set; }
    public string? Reference { get; set; }

    // ─────────────── Relationships ───────────────
    public int CustomerId { get; set; }                 // required — no orphan bookings
    public Customer Customer { get; set; } = null!;

    public int? PackageId { get; set; }                 // optional (custom / bespoke bookings)
    public Package? Package { get; set; }

    public int? ExtendedAreaId { get; set; }
    public Area? ExtendedArea { get; set; }
    public bool ApplyExtendedAmount { get; set; } = false;

    // ─────────────── Event Details ───────────────
    public string? Venue { get; set; }
    public DateOnly EventDate { get; set; }
    public TimeSpan EventStartTime { get; set; }
    public TimeSpan EventEndTime { get; set; }
    public int NumberOfHeads { get; set; }
    public string? SpecialInstructions { get; set; }

    // ═══════════ PRICE SNAPSHOT — IMMUTABLE AFTER CREATION ═══════════
    // Captured at booking time. Never updated when Package price changes.
    public string? PackageName { get; private set; }      // frozen display name
    public decimal PricePerHead { get; private set; }     // frozen rate per person
    public decimal PackageAmount { get; private set; }    // PricePerHead × NoOfPerson (legacy col: p_amount)

    // ─────────────── Financial Breakdown ───────────────
    public decimal Subtotal { get; private set; }
    public DiscountType DiscountType { get; set; } = DiscountType.None;
    public decimal DiscountPercentage { get; set; }
    public decimal DiscountAmount { get; private set; }
    public decimal TaxAmount { get; private set; }
    public decimal AmountDue { get; private set; }
    public decimal DepositAmount { get; private set; }
    public decimal AmountPaid { get; private set; }

    /// <summary>
    /// Computed balance. Always non-negative. A negative would indicate overpayment,
    /// which the domain clamps to zero; the UI/reporting layer can surface overpayments
    /// separately via TotalPayments vs AmountDue.
    /// </summary>
    public decimal Balance => AmountDue > 0 ? Math.Max(0m, AmountDue - AmountPaid) : 0m;

    // ─────────────── Status (single source of truth, replaces ServeStatus + IsCancelled) ───────────────
    /// <summary>
    /// Single authoritative lifecycle status. The boolean pair ServeStatus + IsCancelled
    /// has been removed because it allowed invalid states (both true, both false).
    /// </summary>
    public BookingStatus Status { get; private set; } = BookingStatus.Pending;

    public bool ReminderSent { get; private set; } = false;

    // ─────────────── Concurrency ───────────────
    /// <summary>Row version for optimistic concurrency. Prevents lost-update races on concurrent edits.</summary>
    public byte[]? RowVersion { get; set; }

    // ─────────────── Collections ───────────────
    public ICollection<Payment> Payments { get; set; } = new List<Payment>();
    public ICollection<Refund> Refunds { get; set; } = new List<Refund>();

    // ═══════════════ Domain Behaviour ═══════════════

    private Booking() { } // EF Core constructor

    /// <summary>
    /// Factory method — the only way to create a valid Booking.
    /// Freezes the price snapshot at the moment of creation.
    /// </summary>
    public static Booking Create(
        int customerId,
        int noOfPerson,
        BookingType bookType,
        ServiceType typeOfService,
        DateOnly eventDate,
        DateTime startDate,
        DateTime endDate,
        string? occasion = null,
        string? venue = null,
        string? eventColor = null,
        string? reference = null,
        int? packageId = null,
        string? packageName = null,
        decimal pricePerHead = 0m,
        int? extendedAreaId = null,
        bool applyExtendedAmount = false,
        string? specialInstructions = null)
    {
        if (customerId <= 0)
            throw new DomainException("A valid customer is required.");
        if (noOfPerson <= 0)
            throw new DomainException("Number of persons must be greater than zero.");
        if (endDate < startDate)
            throw new DomainException("Event end date must not be before start date.");

        var booking = new Booking
        {
            CustomerId = customerId,
            NumberOfHeads = noOfPerson,
            BookType = bookType,
            TypeOfService = typeOfService,
            EventDate = eventDate,
            EventStartTime = startDate.TimeOfDay,
            EventEndTime = endDate.TimeOfDay,
            Occasion = occasion,
            Venue = venue,
            EventColor = eventColor ?? "#2563eb",
            Reference = reference,
            PackageId = packageId,
            SpecialInstructions = specialInstructions,
            ExtendedAreaId = extendedAreaId,
            ApplyExtendedAmount = applyExtendedAmount,
            Status = BookingStatus.Pending,
            TransactionDate = DateTime.UtcNow,
        };

        // Freeze the price snapshot
        booking.SetPriceSnapshot(packageName, pricePerHead, noOfPerson);

        return booking;
    }

    /// <summary>
    /// Freezes package name and per-head rate. Called only at creation;
    /// subsequent price-list changes must NOT call this on existing bookings.
    /// </summary>
    private void SetPriceSnapshot(string? packageName, decimal pricePerHead, int noOfPerson)
    {
        PackageName = packageName;
        PricePerHead = pricePerHead;
        PackageAmount = pricePerHead * noOfPerson;
        Subtotal = PackageAmount;
        AmountDue = PackageAmount; // will be recalculated once discounts/addons are applied
    }

    /// <summary>
    /// Assigns the human-readable business number exactly once. Must be called
    /// only after the booking has been persisted, because the format embeds the
    /// IDENTITY-generated <see cref="TransactionId"/>:
    /// <c>BK-{yyyyMMdd}-{TransactionId:D5}</c> (DEC-016).
    /// The guard makes the operation idempotent-safe: a second assignment is a
    /// domain error rather than a silent overwrite of an issued number.
    /// </summary>
    public void AssignBookingNumber()
    {
        if (!string.IsNullOrWhiteSpace(BookingNumber))
            throw new DomainException("Booking number has already been assigned.");
        if (TransactionId <= 0)
            throw new DomainException("Booking number cannot be assigned before the booking is persisted.");

        BookingNumber = string.Create(
            CultureInfo.InvariantCulture,
            $"BK-{TransactionDate:yyyyMMdd}-{TransactionId:D5}");
    }

    /// <summary>
    /// Recalculates the financial totals from frozen base values plus any
    /// discounts or adjustments. Call this after applying discounts/addons —
    /// never update the raw price snapshot columns directly.
    /// </summary>
    public void RecalculateFinancials(
        decimal extendedAreaAmount,
        decimal addonsAmount,
        decimal discountAmount,
        decimal taxAmount)
    {
        Subtotal = PackageAmount + extendedAreaAmount + addonsAmount;
        DiscountAmount = Math.Min(discountAmount, Subtotal);        // cap at subtotal
        TaxAmount = taxAmount;
        AmountDue = Math.Max(0m, Subtotal - DiscountAmount + TaxAmount);
    }

    /// <summary>Records a payment and recomputes the in-entity AmountPaid total.</summary>
    public void RecordPayment(decimal amount)
    {
        if (amount <= 0)
            throw new DomainException("Payment amount must be positive.");
        if (Status == BookingStatus.Cancelled)
            throw new DomainException("Cannot record a payment on a cancelled booking.");

        AmountPaid += amount;
        RefreshPaymentStatus();
    }

    /// <summary>
    /// Synchronises Status with payment progress. The application layer calls
    /// this after loading payments from the database.
    /// </summary>
    public void RefreshPaymentStatus()
    {
        if (Status == BookingStatus.Cancelled) return;

        if (AmountDue > 0 && AmountPaid >= AmountDue)
        {
            Status = BookingStatus.FullyPaid;
        }
        else if (AmountPaid > 0 && Status is BookingStatus.Pending or BookingStatus.Confirmed)
        {
            Status = BookingStatus.DepositPaid;
        }
    }

    /// <summary>Transitions the booking to Confirmed status.</summary>
    public void Confirm()
    {
        if (Status == BookingStatus.Cancelled)
            throw new DomainException("A cancelled booking cannot be confirmed.");
        if (Status == BookingStatus.Completed)
            throw new DomainException("A completed booking cannot be re-confirmed.");

        Status = BookingStatus.Confirmed;
    }

    /// <summary>Marks the event as served / in-progress on the day of the event.</summary>
    public void MarkInProgress()
    {
        if (Status == BookingStatus.Cancelled)
            throw new DomainException("Cannot mark a cancelled booking as in-progress.");

        Status = BookingStatus.InProgress;
    }

    /// <summary>Marks the event as fully served and complete.</summary>
    public void MarkCompleted()
    {
        if (Status == BookingStatus.Cancelled)
            throw new DomainException("Cannot complete a cancelled booking.");

        Status = BookingStatus.Completed;
    }

    /// <summary>Cancels the booking. A cancelled booking cannot be un-cancelled.</summary>
    public void Cancel()
    {
        if (Status == BookingStatus.Cancelled)
            throw new DomainException("Booking is already cancelled.");
        if (Status == BookingStatus.Completed)
            throw new DomainException("A completed booking cannot be cancelled.");

        Status = BookingStatus.Cancelled;
    }

    /// <summary>Records that a reminder notification was sent.</summary>
    public void MarkReminderSent()
    {
        ReminderSent = true;
    }

    /// <summary>
    /// Sets the first deposit amount. This is distinct from the running
    /// AmountPaid total and is used for quotation and contract display.
    /// </summary>
    public void SetDepositAmount(decimal depositAmount)
    {
        if (depositAmount < 0)
            throw new DomainException("Deposit amount cannot be negative.");

        DepositAmount = depositAmount;
    }
}
