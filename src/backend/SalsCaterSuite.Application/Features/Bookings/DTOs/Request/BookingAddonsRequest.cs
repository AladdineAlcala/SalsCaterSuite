using SalsCaterSuite.Domain.Enums;

namespace SalsCaterSuite.Application.Features.Bookings.DTOs.Request;

/// <summary>The request body of <c>PUT /api/bookings/{id}/addons</c> (contract §16.4).</summary>
public class BookingAddonsRequest
{
    /// <summary><c>null</c> is a <c>400</c>; an explicitly empty array clears the lines.</summary>
    public List<BookingAddonRequest>? Addons { get; set; }
}
