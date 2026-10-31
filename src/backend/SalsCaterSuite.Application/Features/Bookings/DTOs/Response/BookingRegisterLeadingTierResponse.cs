namespace SalsCaterSuite.Application.Features.Bookings.DTOs.Response;

/// <summary>The leading tier, its share and its period change (KPI-34, DEC-109).</summary>
public class BookingRegisterLeadingTierResponse
{
    public string? PackageType { get; set; }
    public string? PackageName { get; set; }
    public decimal NetContractedValue { get; set; }

    /// <summary>The tier's share of the population's net contracted value.</summary>
    public decimal? SharePercentage { get; set; }

    /// <summary>
    /// The change against the immediately preceding equal-length window. <c>null</c> when the
    /// preceding window holds no booking of this tier — unknown, never zero (DEC-109).
    /// </summary>
    public decimal? ChangePercentage { get; set; }

    /// <summary>The leading tier's net contracted value in the preceding window.</summary>
    public decimal? PreviousNetContractedValue { get; set; }
}
