using SalsCaterSuite.Domain.Entities;

namespace SalsCaterSuite.Application.Services;

public record BookingCalculationBreakdown(
    decimal BasePackageAmount,
    decimal ExtendedAreaAmount,
    decimal AddonsAmount,
    decimal OtherChargesAmount,
    decimal GrossAmount,
    decimal DiscountAmount,
    decimal NetAmountDue,
    decimal TotalPayments,
    decimal RemainingBalance,
    bool IsFullyPaid
);

public class BookingAmountDueCalculator
{
    public BookingCalculationBreakdown Calculate(
        decimal? packageAmount,
        int numberOfHeads,
        decimal pricePerPax,
        decimal extendedAreaRate,
        bool applyExtendedArea,
        decimal addonsTotal,
        decimal otherChargesTotal,
        decimal discountPercentage,
        decimal? discountFixedAmount,
        decimal totalPaymentsReceived)
    {
        decimal basePkg = packageAmount ?? ((pricePerPax) * (numberOfHeads));
        decimal areaCharge = applyExtendedArea ? extendedAreaRate : 0m;
        decimal gross = basePkg + areaCharge + addonsTotal + otherChargesTotal;

        decimal discount = 0m;
        if (discountFixedAmount.HasValue && discountFixedAmount.Value > 0)
        {
            discount = discountFixedAmount.Value;
        }
        else if (discountPercentage > 0)
        {
            discount = (gross * discountPercentage) / 100m;
        }

        if (discount > gross)
        {
            discount = gross;
        }

        decimal netDue = gross - discount;
        decimal balance = netDue - totalPaymentsReceived;
        if (balance < 0) balance = 0;

        bool isFullyPaid = totalPaymentsReceived >= netDue && netDue > 0;

        return new BookingCalculationBreakdown(
            basePkg,
            areaCharge,
            addonsTotal,
            otherChargesTotal,
            gross,
            discount,
            netDue,
            totalPaymentsReceived,
            balance,
            isFullyPaid
        );
    }

    /// <summary>
    /// The single authoritative net-amount-due path for a persisted booking (DEC-042, AC-B3).
    /// <para>
    /// Every consumer that needs a booking's net amount due or remaining balance — the payment
    /// ledger, the booking details view, the calendar feed and the operations dashboard — calls
    /// this overload, so the argument set exists in exactly one place and a dashboard-vs-ledger
    /// divergence is structurally impossible. The argument set is deliberately identical to the
    /// one the ledger has always used:
    /// </para>
    /// <list type="bullet">
    /// <item><description><c>packageAmount</c> is the <b>frozen stored column</b>
    /// (<c>Booking.PackageAmount</c>), never a recomputed <c>PricePerHead × NumberOfHeads</c>.</description></item>
    /// <item><description><c>discountFixedAmount</c> converts a zero stored discount to
    /// <c>null</c>. The calculator gives the fixed discount precedence and only falls back to the
    /// percentage when the fixed value is <c>null</c> or <c>≤ 0</c>; passing the stored
    /// <c>0</c> directly would silently change which discount rule applies.</description></item>
    /// <item><description><c>applyExtendedArea</c> is passed even though
    /// <c>extendedAreaRate</c> is <c>0m</c>, so the call stays textually identical to the
    /// ledger's and cannot drift later.</description></item>
    /// </list>
    /// <para>
    /// <paramref name="totalPaymentsReceived"/> must be the authoritative payment sum
    /// (<c>Σ Payment.AmountPaid</c>, DEC-012) — never the denormalized
    /// <c>Booking.AmountPaid</c> cache.
    /// </para>
    /// </summary>
    public BookingCalculationBreakdown Calculate(Booking booking, decimal totalPaymentsReceived)
    {
        return Calculate(
            packageAmount: booking.PackageAmount,
            numberOfHeads: booking.NumberOfHeads,
            pricePerPax: booking.PricePerHead,
            extendedAreaRate: 0m,
            applyExtendedArea: booking.ApplyExtendedAmount,
            addonsTotal: 0m,
            otherChargesTotal: 0m,
            discountPercentage: booking.DiscountPercentage,
            discountFixedAmount: booking.DiscountAmount > 0 ? booking.DiscountAmount : null,
            totalPaymentsReceived: totalPaymentsReceived
        );
    }
}
