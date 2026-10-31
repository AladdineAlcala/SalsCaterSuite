using SalsCaterSuite.Domain.Enums;

namespace SalsCaterSuite.Application.Features.Bookings.DTOs.Request;

/// <summary>One submitted add-on line. The request carries no quantity and no basis.</summary>
public class BookingAddonRequest
{
    /// <summary>Required, and must be an <b>active</b> catalogue row.</summary>
    public int AddonId { get; set; }

    /// <summary>
    /// Optional <b>line total</b> (DEC-158). When absent the server derives
    /// <c>catalogueAmount x heads</c> for a <c>PerGuest</c> add-on and <c>catalogueAmount</c> for
    /// a <c>Flat</c> one.
    /// </summary>
    public decimal? Amount { get; set; }
}
