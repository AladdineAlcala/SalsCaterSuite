using SalsCaterSuite.Domain.Enums;

namespace SalsCaterSuite.Application.Features.Bookings.DTOs.Response;

/// <summary>
/// The register's totals strip. Every figure is over the <b>whole filtered population</b>,
/// never over the page (BR-32).
/// </summary>
public class BookingRegisterTotalsResponse
{
    /// <summary>The number of bookings in the filtered population.</summary>
    public int BookingCount { get; set; }

    /// <summary>The sum of the net amount due — the net contracted value (KPI-35).</summary>
    public decimal NetContractedValue { get; set; }

    /// <summary>The sum of the collected amounts (KPI-35).</summary>
    public decimal CollectedAmount { get; set; }

    /// <summary>The sum of the outstanding balances (KPI-35, KPI-39).</summary>
    public decimal OutstandingAmount { get; set; }

    /// <summary>The sum of the headcounts (KPI-36).</summary>
    public int TotalPax { get; set; }
}
