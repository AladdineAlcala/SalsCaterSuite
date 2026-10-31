using SalsCaterSuite.Domain.Enums;

namespace SalsCaterSuite.Application.Features.Bookings.DTOs.Response;

/// <summary>One chosen dish of the proposed selection (contract §14.3).</summary>
public class BookingQuoteSelectedMenuResponse
{
    public string MenuId { get; set; } = string.Empty;
    public string? MenuName { get; set; }
    public string? Note { get; set; }
    public string? PrepInstruction { get; set; }
    public decimal? FoodCost { get; set; }
}
