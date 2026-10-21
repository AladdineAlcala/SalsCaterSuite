namespace SalsCaterSuite.Application.Features.Customers.DTOs;

public class CustomerDto
{
    public int CustomerId { get; set; }
    public string LastName { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string? Middle { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? Address { get; set; }
    public string? Contact1 { get; set; }
    public string? Contact2 { get; set; }
    public string? Company { get; set; }
    public DateTime? DateRegistered { get; set; }
    public int TotalBookings { get; set; }
    public decimal TotalReceivables { get; set; }

    // ── Customer Master extensions (spec §5.2) ──────────────────────────────────────────────
    // The fields below are populated by the Customer Master's own projections. The legacy
    // unpaged list (GET /api/customers) keeps its historic shape, so they are nullable there
    // and always populated by GET /api/customers/{id}.

    /// <summary>
    /// The derived account classification: <c>Corporate</c> when the company name is not blank,
    /// <c>Private</c> otherwise (spec §4.4, DEC-051). Never persisted.
    /// </summary>
    public string? CustomerType { get; set; }

    /// <summary>The customer's booking counters.</summary>
    public CustomerBookingCountsDto? BookingCounts { get; set; }

    /// <summary>KPI-21 — <c>Σ NetAmountDue</c> over the customer's non-cancelled bookings.</summary>
    public decimal? TotalContractedValue { get; set; }

    /// <summary><c>Σ Payment.AmountPaid</c> over the customer's non-cancelled bookings (DEC-012).</summary>
    public decimal? TotalPaid { get; set; }

    /// <summary>
    /// <c>Σ max(0, NetAmountDue − AmountPaid)</c> over the customer's non-cancelled bookings
    /// (DEC-025, BR-03). It is the same figure as <see cref="TotalReceivables"/>.
    /// </summary>
    public decimal? OutstandingBalance { get; set; }

    /// <summary>The aging bucket key, e.g. <c>Clean</c> or <c>Days31To60</c>.</summary>
    public string? LedgerState { get; set; }

    /// <summary>The server-provided display label of <see cref="LedgerState"/>.</summary>
    public string? LedgerStateLabel { get; set; }

    /// <summary>
    /// The customer's next non-cancelled booking with an event date of today or later
    /// (spec §4.8). <c>null</c> when the customer has no upcoming booking.
    /// </summary>
    public CustomerUpcomingBookingDto? UpcomingBooking { get; set; }
}

/// <summary>
/// The customer's next upcoming booking (spec §4.8).
/// </summary>
public class CustomerUpcomingBookingDto
{
    public int TransactionId { get; set; }

    /// <summary>The contractual booking number (BR-09, DEC-016).</summary>
    public string BookingNumber { get; set; } = string.Empty;

    public string Occasion { get; set; } = string.Empty;

    public DateOnly EventDate { get; set; }

    /// <summary>
    /// The booking's net amount due, computed through the single shared calculator overload
    /// (DEC-046) — never the persisted <c>Booking.AmountDue</c> column.
    /// </summary>
    public decimal AmountDue { get; set; }
}

public class CustomerStatementDto
{
    public int CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string? Contact { get; set; }
    public string? Address { get; set; }
    public decimal TotalBilled { get; set; }
    public decimal TotalPaid { get; set; }

    /// <summary>
    /// <c>Σ Refund.NetAmount</c> over the customer's non-cancelled bookings. Refunds are
    /// reported separately and never reduce <see cref="OutstandingBalance"/> (DEC-013).
    /// </summary>
    public decimal TotalRefunded { get; set; }

    public decimal OutstandingBalance { get; set; }

    /// <summary>The customer's aging state key (spec §3, DEC-052).</summary>
    public string LedgerState { get; set; } = string.Empty;

    /// <summary>The server-provided display label of <see cref="LedgerState"/>.</summary>
    public string LedgerStateLabel { get; set; } = string.Empty;

    public List<CustomerStatementLineDto> Transactions { get; set; } = new();
}

public class CustomerStatementLineDto
{
    public int TransactionId { get; set; }

    /// <summary>The contractual booking number (BR-09, DEC-016).</summary>
    public string BookingNumber { get; set; } = string.Empty;

    public DateTime? EventDate { get; set; }
    public string Occasion { get; set; } = string.Empty;

    /// <summary>
    /// The booking's net amount due, computed through the single shared calculator overload
    /// (DEC-046) — never the persisted <c>Booking.AmountDue</c> or the frozen
    /// <c>Booking.PackageAmount</c> column (spec §7.1, F11 / RSK-009).
    /// </summary>
    public decimal Amount { get; set; }

    /// <summary><c>Σ Payment.AmountPaid</c> — the authoritative payment sum (DEC-012).</summary>
    public decimal Paid { get; set; }

    /// <summary><c>max(0, Amount − Paid)</c> — never negative (BR-03).</summary>
    public decimal Balance { get; set; }

    /// <summary>
    /// The approved BR-10 booking lifecycle name, e.g. <c>Completed</c> or <c>Confirmed</c>.
    /// The retired "Served" / "Upcoming" vocabulary is not used (spec §7.2).
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// The DEC-041 aging bucket of the line, or <c>null</c> when the line carries no balance.
    /// </summary>
    public string? AgingBucket { get; set; }

    /// <summary>The server-provided display label of <see cref="AgingBucket"/>.</summary>
    public string? AgingBucketLabel { get; set; }
}

/// <summary>Request body for creating or updating a customer.</summary>
public record CreateCustomerRequestDto(
    string LastName,
    string FirstName,
    string? Middle,
    string? Address,
    string? Contact1,
    string? Contact2,
    string? Company);
