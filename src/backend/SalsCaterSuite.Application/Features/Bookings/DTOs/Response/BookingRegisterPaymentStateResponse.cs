namespace SalsCaterSuite.Application.Features.Bookings.DTOs.Response;

/// <summary>One payment state's count and its outstanding amount (KPI-37).</summary>
public class BookingRegisterPaymentStateResponse
{
    public string Key { get; set; } = string.Empty;
    public int Count { get; set; }
    public decimal Amount { get; set; }
}
