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

    // ─────────────── Dispatch Schedule (DEC-106) ───────────────
    /// <summary>
    /// The kitchen call time. Frozen at creation (DEC-106). <c>null</c> = not stated.
    /// </summary>
    public TimeSpan? CallTime { get; private set; }

    /// <summary>
    /// The pack-out time. Frozen at creation (DEC-106). <c>null</c> = not stated.
    /// </summary>
    public TimeSpan? PackoutTime { get; private set; }

    // ─────────────── Event Details ───────────────
    public string? Venue { get; set; }
    public DateOnly EventDate { get; set; }

    /// <summary>
    /// The calendar date the event ends. Equal to <see cref="EventDate"/> for a same-day event.
    /// An overnight event stores the following date so pack-out can fall after midnight (DEC-178).
    /// </summary>
    public DateOnly EventEndDate { get; set; }

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
    /// The <b>frozen</b> code of the discount that was applied (DEC-106, BR-02).
    /// <para>
    /// The discount's <b>amount</b> was already stored; the <b>code</b> is what makes the
    /// booking auditable — it says <i>which</i> discount was applied, so a later change to the
    /// discount catalogue can never alter the stored booking (BR-04). <c>null</c> = no discount
    /// was applied.
    /// </para>
    /// </summary>
    public string? DiscountCode { get; private set; }

    /// <summary>
    /// The <b>frozen</b> out-of-town surcharge in pesos (BR-26, DEC-116).
    /// <para>
    /// This is a column of its own and <b>not</b> an inference from
    /// <c>Subtotal − PackageAmount</c>: <see cref="Subtotal"/> conflates the surcharge with
    /// the add-on total and the other charges, and <see cref="ApplyExtendedAmount"/> is a
    /// creation-time switch rather than a money switch. The stored amount is what the shared
    /// calculator charges, so flipping the flag after the booking exists can never move it.
    /// </para>
    /// </summary>
    public decimal ExtendedAreaAmount { get; private set; }

    /// <summary>
    /// The <b>frozen</b> add-on total in pesos (BR-44, DEC-158).
    /// <para>
    /// Like <see cref="ExtendedAreaAmount"/>, this is a column of its own and <b>not</b> an
    /// inference from <c>Subtotal − PackageAmount − ExtendedAreaAmount</c>: the stored amount is
    /// what the shared calculator charges, so later edits to the add-on catalogue can never move
    /// an existing booking's economics (BR-04).
    /// </para>
    /// </summary>
    public decimal AddonsTotal { get; private set; }

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

    /// <summary>
    /// Derived payment progress (DEC-177). Written only by <see cref="RefreshPaymentStatus(decimal, decimal)"/>
    /// from the non-soft-deleted payment sum and the net amount due. A payment never changes
    /// <see cref="Status"/>.
    /// </summary>
    public PaymentState PaymentState { get; private set; } = PaymentState.PendingPayment;

    public bool ReminderSent { get; private set; } = false;

    // ─────────────── Concurrency ───────────────
    /// <summary>Row version for optimistic concurrency. Prevents lost-update races on concurrent edits.</summary>
    public byte[]? RowVersion { get; set; }

    // ─────────────── Collections ───────────────
    public ICollection<Payment> Payments { get; set; } = new List<Payment>();
    public ICollection<Refund> Refunds { get; set; } = new List<Refund>();

    /// <summary>The frozen dish selection — one row per selected dish (BR-35, DEC-155).</summary>
    public ICollection<BookingMenu> Menus { get; set; } = new List<BookingMenu>();

    /// <summary>The frozen add-on lines (BR-44, DEC-158).</summary>
    public ICollection<BookingAddOn> AddOns { get; set; } = new List<BookingAddOn>();

    /// <summary>Durable lifecycle-status facts (DEC-178). Payment state is not recorded here.</summary>
    public ICollection<BookingStatusHistory> StatusHistory { get; set; } = new List<BookingStatusHistory>();

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
        string? specialInstructions = null,
        TimeSpan? callTime = null,
        TimeSpan? packoutTime = null,
        string? discountCode = null,
        decimal extendedAreaAmount = 0m)
    {
        if (customerId <= 0)
            throw new DomainException("A valid customer is required.");
        if (noOfPerson <= 0)
            throw new DomainException("Number of persons must be greater than zero.");
        if (endDate < startDate)
            throw new DomainException("Event end date must not be before start date.");
        if (extendedAreaAmount < 0m)
            throw new DomainException("The extended area amount cannot be negative.");

        var eventEndDate = DateOnly.FromDateTime(endDate);
        if (eventEndDate < eventDate)
            throw new DomainException("Event end date must not be before the event date.");

        var booking = new Booking
        {
            CustomerId = customerId,
            NumberOfHeads = noOfPerson,
            BookType = bookType,
            TypeOfService = typeOfService,
            EventDate = eventDate,
            EventEndDate = eventEndDate,
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
            CallTime = callTime,
            PackoutTime = packoutTime,
            DiscountCode = discountCode,
            Status = BookingStatus.Pending,
            TransactionDate = DateTime.UtcNow,
        };

        // Freeze the price snapshot
        booking.SetPriceSnapshot(packageName, pricePerHead, noOfPerson, extendedAreaAmount);
        booking.RefreshPaymentStatus(0m, booking.AmountDue);

        return booking;
    }

    /// <summary>
    /// Freezes package name and per-head rate. Called only at creation;
    /// subsequent price-list changes must NOT call this on existing bookings.
    /// </summary>
    private void SetPriceSnapshot(string? packageName, decimal pricePerHead, int noOfPerson, decimal extendedAreaAmount)
    {
        PackageName = packageName;
        PricePerHead = pricePerHead;
        PackageAmount = pricePerHead * noOfPerson;
        // The frozen out-of-town surcharge (BR-26, DEC-116) — a column of its own, charged
        // exactly once by the shared calculator.
        ExtendedAreaAmount = extendedAreaAmount;
        Subtotal = PackageAmount + extendedAreaAmount;
        AmountDue = Subtotal; // will be recalculated once discounts/addons are applied
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
        // The frozen surcharge is written once, here, and read directly by the shared
        // calculator (BR-26, DEC-116). It is never inferred from the subtotal.
        ExtendedAreaAmount = extendedAreaAmount;
        AddonsTotal = addonsAmount;
        Subtotal = PackageAmount + extendedAreaAmount + addonsAmount;
        DiscountAmount = Math.Min(discountAmount, Subtotal);        // cap at subtotal
        TaxAmount = taxAmount;
        AmountDue = Math.Max(0m, Subtotal - DiscountAmount + TaxAmount);
        RefreshPaymentStatus(AmountPaid, AmountDue);
    }

    /// <summary>Records a payment and recomputes the in-entity AmountPaid total.</summary>
    public void RecordPayment(decimal amount)
    {
        if (amount <= 0)
            throw new DomainException("Payment amount must be positive.");
        if (Status == BookingStatus.Cancelled)
            throw new DomainException("Cannot record a payment on a cancelled booking.");

        AmountPaid += amount;
        // Provisional refresh from the denormalized cache. Callers that hold the authoritative
        // non-soft-deleted payment sum overwrite this before save (DEC-177). Lifecycle status
        // is intentionally untouched.
        RefreshPaymentStatus(AmountPaid, AmountDue);
    }

    /// <summary>
    /// Sets <see cref="PaymentState"/> from the authoritative collected sum and net amount due.
    /// Does not read or write <see cref="Status"/> (DEC-177).
    /// </summary>
    public void RefreshPaymentStatus(decimal collected, decimal netAmountDue)
    {
        PaymentState = PaymentStateRules.Classify(collected, netAmountDue);
    }

    /// <summary>
    /// Sets <see cref="PaymentState"/> from the loaded non-soft-deleted payment rows and the
    /// stored net amount due. Call only when <see cref="Payments"/> has been loaded.
    /// </summary>
    public void RefreshPaymentStatus()
    {
        var collected = Payments.Where(p => !p.IsDeleted).Sum(p => p.AmountPaid);
        RefreshPaymentStatus(collected, AmountDue);
    }

    /// <summary>
    /// Applies one BG-04 edge and nothing else. Returns false when the booking is soft-deleted
    /// or is not in <paramref name="from"/>, so a repeat or a lost race is a no-op.
    /// </summary>
    public bool TryApplyScheduledTransition(BookingStatus from, BookingStatus to)
    {
        if (IsDeleted || Status != from)
        {
            return false;
        }

        if (from == BookingStatus.Confirmed && to == BookingStatus.InProgress)
        {
            Status = BookingStatus.InProgress;
            return true;
        }

        if (from == BookingStatus.InProgress && to == BookingStatus.Completed)
        {
            Status = BookingStatus.Completed;
            return true;
        }

        return false;
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
        if (Status == BookingStatus.NoShow)
            throw new DomainException("Cannot complete a no-show booking.");
        if (Status == BookingStatus.Rescheduled)
            throw new DomainException("Cannot complete a rescheduled booking.");

        if (Status == BookingStatus.Completed)
            return;

        Status = BookingStatus.Completed;
    }

    /// <summary>Updates the booking core details that are editable after creation.</summary>
    public void UpdateCoreDetails(DateOnly eventDate, int numberOfHeads)
    {
        if (eventDate == default)
            throw new DomainException("Event date is required.");
        if (numberOfHeads <= 0)
            throw new DomainException("Number of heads must be greater than zero.");
        if (Status is BookingStatus.Cancelled or BookingStatus.Completed or BookingStatus.NoShow or BookingStatus.Rescheduled)
            throw new DomainException("This booking cannot be edited in its current lifecycle state.");

        // Preserve the event length. The core write has no separate end date, and leaving
        // EventEndDate behind would let BG-04 complete an event before it starts (DEC-178).
        var durationDays = Math.Max(0, EventEndDate.DayNumber - EventDate.DayNumber);
        EventDate = eventDate;
        EventEndDate = eventDate.AddDays(durationDays);
        NumberOfHeads = numberOfHeads;

        if (PricePerHead > 0m)
        {
            PackageAmount = PricePerHead * NumberOfHeads;
        }

        var updatedSubtotal = PackageAmount + ExtendedAreaAmount + AddonsTotal;
        Subtotal = updatedSubtotal;
        AmountDue = Math.Max(0m, updatedSubtotal - DiscountAmount + TaxAmount);
        RefreshPaymentStatus(AmountPaid, AmountDue);
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
