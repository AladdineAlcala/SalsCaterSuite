namespace SalsCaterSuite.Application.Features.Customers.DTOs.Response;

/// <summary>
/// One row of the customer directory (spec §4.5).
/// </summary>
public class CustomerDirectoryItemResponse
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
    public CustomerBookingCountsResponse BookingCounts { get; set; } = new();

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
