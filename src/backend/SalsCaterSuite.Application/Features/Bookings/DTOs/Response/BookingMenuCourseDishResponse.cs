using SalsCaterSuite.Domain.Enums;

namespace SalsCaterSuite.Application.Features.Bookings.DTOs.Response;

/// <summary>One live dish of a qualified pool (contract §15.1).</summary>
public class BookingMenuCourseDishResponse
{
    public string MenuId { get; set; } = string.Empty;
    public string? MenuName { get; set; }
    public string? Note { get; set; }
    public decimal? FoodCost { get; set; }
    public decimal? Price { get; set; }
}
