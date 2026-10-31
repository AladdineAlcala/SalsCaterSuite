namespace SalsCaterSuite.Application.Features.Customers.DTOs.Response;

public class CustomerStatementLineResponse
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
