namespace SalsCaterSuite.Application.Features.Bookings.DTOs.Response;

/// <summary>The committed headcount of the population (KPI-36).</summary>
public class BookingRegisterHeadcountResponse
{
    public int TotalPax { get; set; }

    /// <summary>The mean headcount per booking; <c>null</c> for an empty population.</summary>
    public decimal? AveragePaxPerBooking { get; set; }

    /// <summary>The next event's date; <c>null</c> when the population has no future event.</summary>
    public DateOnly? NextEventDate { get; set; }

    /// <summary>The next event's booking number.</summary>
    public string? NextEventBookingNumber { get; set; }
}
