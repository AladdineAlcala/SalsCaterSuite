namespace SalsCaterSuite.Application.Features.Customers.DTOs.Response;

public class CustomerResponse
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
    public CustomerBookingCountsResponse? BookingCounts { get; set; }

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
    public CustomerUpcomingBookingResponse? UpcomingBooking { get; set; }
}
