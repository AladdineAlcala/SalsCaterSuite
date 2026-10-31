using SalsCaterSuite.Domain.Enums;

namespace SalsCaterSuite.Application.Features.Bookings.DTOs.Request;

/// <summary>One submitted dish (contract §15.2).</summary>
public class BookingMenuSelectionEntryRequest
{
    public int CourseId { get; set; }
    public string MenuId { get; set; } = string.Empty;

    /// <summary>Optional free text of at most 500 characters (DEC-146, DEC-155).</summary>
    public string? PrepInstruction { get; set; }
}
