namespace SalsCaterSuite.Application.Features.Customers.DTOs.Response;

/// <summary>
/// The customer's next upcoming booking (spec §4.8).
/// </summary>
public class CustomerUpcomingBookingResponse
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
