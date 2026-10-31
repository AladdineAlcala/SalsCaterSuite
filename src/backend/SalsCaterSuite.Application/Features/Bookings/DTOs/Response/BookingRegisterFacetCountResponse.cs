using SalsCaterSuite.Domain.Enums;

namespace SalsCaterSuite.Application.Features.Bookings.DTOs.Response;

/// <summary>One facet entry — a key and the number of bookings behind it.</summary>
public class BookingRegisterFacetCountResponse
{
    /// <summary>The facet key — a tier type, a venue, a status name or a payment-state name.</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>How many bookings carry the key.</summary>
    public int Count { get; set; }
}
