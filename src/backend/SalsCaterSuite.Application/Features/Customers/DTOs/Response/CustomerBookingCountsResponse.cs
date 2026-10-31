namespace SalsCaterSuite.Application.Features.Customers.DTOs.Response;

/// <summary>
/// A customer's booking counters (spec §4.5).
/// </summary>
public class CustomerBookingCountsResponse
{
    /// <summary>The bookings that are not cancelled.</summary>
    public int Total { get; set; }

    /// <summary>The non-cancelled bookings whose event date is in the past.</summary>
    public int Past { get; set; }

    /// <summary>The non-cancelled bookings whose event date is today or later.</summary>
    public int Upcoming { get; set; }

    /// <summary>The bookings whose lifecycle status is <c>Completed</c>.</summary>
    public int Completed { get; set; }

    /// <summary>The bookings whose lifecycle status is <c>Cancelled</c>.</summary>
    public int Cancelled { get; set; }
}
