using SalsCaterSuite.Application.Features.Bookings.DTOs.Response;
using SalsCaterSuite.Domain.Enums;

namespace SalsCaterSuite.Application.Features.Bookings.Models;

/// <summary>
/// The register's unpaged result set — the whole filtered population and its totals.
/// <para>
/// The XLSX export carries the very same filters as the register, but the <b>whole</b>
/// population and never the current page (contract §14.6).
/// </para>
/// </summary>
public class BookingRegisterExportData
{
    /// <summary>Every row of the filtered population, in the register's own order.</summary>
    public List<BookingRegisterItemResponse> Items { get; set; } = new();

    /// <summary>The totals over the whole filtered population.</summary>
    public BookingRegisterTotalsResponse Totals { get; set; } = new();
}
