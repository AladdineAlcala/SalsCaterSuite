using SalsCaterSuite.Domain.Enums;

namespace SalsCaterSuite.Application.Features.Bookings.DTOs.Response;

/// <summary>
/// The municipality coverage verdict (BR-29). <c>IsCovered</c> is <c>null</c> when the tier
/// has no coverage set — unknown, never a breach.
/// </summary>
public class BookingQuoteCoverageResponse
{
    /// <summary><c>null</c> when the tier carries no coverage set.</summary>
    public bool? IsCovered { get; set; }

    /// <summary>How many municipalities the tier covers.</summary>
    public int CoveredAreaCount { get; set; }
}
