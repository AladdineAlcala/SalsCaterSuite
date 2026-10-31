using SalsCaterSuite.Domain.Enums;

namespace SalsCaterSuite.Application.Features.Bookings.DTOs.Request;

/// <summary>
/// The request body of <c>PUT /api/bookings/{id}/menus</c> — the same shape as the packages'
/// selection simulator, with the chef's per-event instruction added (contract §15.2).
/// </summary>
public class BookingMenuSelectionRequest
{
    /// <summary>
    /// <c>null</c> is a <c>400</c> (the DEC-085 pattern). An explicitly empty array clears the
    /// selection, subject to the lock.
    /// </summary>
    public List<BookingMenuSelectionEntryRequest>? Selections { get; set; }
}
