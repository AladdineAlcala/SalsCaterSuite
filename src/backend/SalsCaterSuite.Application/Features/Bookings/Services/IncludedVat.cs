using SalsCaterSuite.Application.Services;

namespace SalsCaterSuite.Application.Features.Bookings.Services;

/// <summary>
/// The <b>VAT-inclusive disclosure</b> (BR-42, DEC-143).
/// <para>
/// The price the client pays is VAT-inclusive, so the VAT is a memo line <b>inside</b> the
/// total and <b>never</b> an addition to it:
/// <c>IncludedVatAmount = NetAmountDue - NetAmountDue / (1 + VatPercentage / 100)</c>.
/// It is <c>null</c> when the rate is not configured or the net amount due is zero. With the
/// key configured, the formula is applied literally — a configured 0 publishes 0.
/// </para>
/// </summary>
public static class IncludedVat
{
    /// <summary>
    /// The disclosed VAT amount, or <c>null</c> when the rate is absent or the net is zero.
    /// </summary>
    public static decimal? Calculate(decimal netAmountDue, decimal? vatPercentage)
    {
        if (!vatPercentage.HasValue || netAmountDue == 0m)
        {
            return null;
        }

        var divisor = 1m + vatPercentage.Value / 100m;

        // A rate of exactly -100 would make the divisor zero; the configuration is not a
        // business rule and a nonsensical rate must not take the surface down.
        if (divisor == 0m)
        {
            return null;
        }

        return BookingAmountDueCalculator.Round2(
            netAmountDue - netAmountDue / divisor);
    }
}
