namespace SalsCaterSuite.Application.Features.Bookings.Services;

/// <summary>
/// The near-term commissary window (KPI-38, DEC-102): the next <b>14 days</b>, today
/// included.
/// <para>
/// The register summary and the workbench quote both read the load over the same window, so
/// the gauge on the workbench and the gauge on the register can never describe different
/// windows. The window is a <b>date basis</b>, not a business rule — the capacity itself is
/// configuration and absent by default.
/// </para>
/// </summary>
public static class BookingCapacityWindow
{
    /// <summary>The number of days the near-term load covers.</summary>
    public const int Days = 14;
}
