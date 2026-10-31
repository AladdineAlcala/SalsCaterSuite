using SalsCaterSuite.Domain.Enums;

namespace SalsCaterSuite.Application.Features.Bookings.DTOs.Response;

/// <summary>One chosen dish (contract §15.1).</summary>
public class BookingMenuSelectionItemResponse
{
    public int CourseId { get; set; }
    public string? CourseName { get; set; }
    public string MenuId { get; set; } = string.Empty;
    public string? MenuName { get; set; }
    public string? Note { get; set; }
    public decimal? FoodCost { get; set; }

    /// <summary>The chef's per-event instruction for this dish (DEC-146); <c>null</c> = not stated.</summary>
    public string? PrepInstruction { get; set; }
}
