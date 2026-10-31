using SalsCaterSuite.Domain.Enums;

namespace SalsCaterSuite.Application.Features.Bookings.DTOs.Response;

/// <summary>
/// The register's facet counts (BR-32). Each facet is computed with <b>its own dimension
/// removed</b>, so every facet answers "how many would I get if I chose this?".
/// </summary>
public class BookingRegisterFacetsResponse
{
    /// <summary>The tier-type counts.</summary>
    public List<BookingRegisterFacetCountResponse> PackageTypes { get; set; } = new();

    /// <summary>The venue counts.</summary>
    public List<BookingRegisterFacetCountResponse> Venues { get; set; } = new();

    /// <summary>The lifecycle-status counts.</summary>
    public List<BookingRegisterFacetCountResponse> Statuses { get; set; } = new();

    /// <summary>The payment-state counts.</summary>
    public List<BookingRegisterFacetCountResponse> PaymentStates { get; set; } = new();
}
