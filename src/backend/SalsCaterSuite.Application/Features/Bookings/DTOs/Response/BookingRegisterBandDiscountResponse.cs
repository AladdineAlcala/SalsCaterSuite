namespace SalsCaterSuite.Application.Features.Bookings.DTOs.Response;

/// <summary>
/// The automatic headcount band's take (KPI-42, DEC-154). The count is never <c>null</c>; it is
/// <c>0</c> for an empty population.
/// </summary>
public class BookingRegisterBandDiscountResponse
{
    public int DiscountedBookingCount { get; set; }

    /// <summary>The net amount due of the bookings the band priced.</summary>
    public decimal DiscountedNetAmountDue { get; set; }
}
