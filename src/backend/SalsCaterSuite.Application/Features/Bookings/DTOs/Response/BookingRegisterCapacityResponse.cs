namespace SalsCaterSuite.Application.Features.Bookings.DTOs.Response;

/// <summary>The near-term commissary load against the configured capacity (KPI-38, DEC-102).</summary>
public class BookingRegisterCapacityResponse
{
    /// <summary>The pax committed in the next 14 days.</summary>
    public int CommittedPax { get; set; }

    /// <summary>
    /// The configured capacity. <c>null</c> when the key is absent — unknown, never zero
    /// (DEC-102).
    /// </summary>
    public int? CapacityPax { get; set; }

    /// <summary>The utilisation; <c>null</c> when the capacity is unknown.</summary>
    public decimal? UtilisationPercentage { get; set; }

    /// <summary>Whether a capacity has been configured at all.</summary>
    public bool IsConfigured { get; set; }
}
