using SalsCaterSuite.Domain.Enums;

namespace SalsCaterSuite.Application.Features.Bookings.DTOs.Response;

/// <summary>
/// Response body for POST /api/bookings (201 Created).
/// <para>
/// The body was extended by REQ-BKG-05 with the <b>booking number</b> (BR-09, DEC-016) and the
/// <b>status</b>; the <c>transactionId</c> field is unchanged, so the extension is additive.
/// </para>
/// </summary>
public class CreateBookingResponse
{
    /// <summary>The booking's surrogate identifier.</summary>
    public int TransactionId { get; set; }

    /// <summary>The contractual booking number — <c>BK-YYYYMMDD-NNNNN</c>.</summary>
    public string BookingNumber { get; set; } = string.Empty;

    /// <summary>The lifecycle status the booking was created in.</summary>
    public BookingStatus Status { get; set; } = BookingStatus.Pending;
}
