namespace SalsCaterSuite.Application.Features.Bookings.Models;

/// <summary>
/// The raw, transport-bound filter of the Bookings &amp; Events Register (contract §14.1).
/// <para>
/// The values arrive as they were sent: the status and the payment state are comma-separated
/// lists of names, and the vocabulary is checked by the Application layer — an unknown name is
/// a <c>400</c>, never a silently widened filter (BR-32).
/// </para>
/// </summary>
public class BookingRegisterFilterRequest
{
    /// <summary>
    /// The search term: a case-insensitive match over the booking number, the occasion, the
    /// customer name, the customer company and the venue. Empty matches everything.
    /// </summary>
    public string? Search { get; set; }

    /// <summary>The comma-separated <c>BookingStatus</c> names to include.</summary>
    public string? Status { get; set; }

    /// <summary>The comma-separated stored tier types to include.</summary>
    public string? PackageType { get; set; }

    /// <summary>The comma-separated stored venues to include.</summary>
    public string? Venue { get; set; }

    /// <summary>The comma-separated payment-state names to include.</summary>
    public string? PaymentState { get; set; }

    /// <summary>
    /// The overdue filter — a <b>tri-state</b> (BR-32):
    /// <list type="bullet">
    /// <item><description><b>absent</b> — no overdue filter at all.</description></item>
    /// <item><description><b><c>true</c></b> — only the bookings whose balance is outstanding
    /// and whose event date has passed (DEC-041).</description></item>
    /// <item><description><b><c>false</c></b> — only the bookings that are <b>not</b> overdue.
    /// An explicit <c>false</c> narrows the population; it is never silently treated as
    /// absent.</description></item>
    /// </list>
    /// </summary>
    public bool? Overdue { get; set; }

    /// <summary>The inclusive lower bound on the event date.</summary>
    public DateOnly? FromDate { get; set; }

    /// <summary>The inclusive upper bound on the event date.</summary>
    public DateOnly? ToDate { get; set; }

    /// <summary>The 1-based page number.</summary>
    public int Page { get; set; } = 1;

    /// <summary>The page size — 10, 25 or 50.</summary>
    public int PageSize { get; set; } = 10;

    /// <summary>The requested sort.</summary>
    public string? Sort { get; set; }
}
