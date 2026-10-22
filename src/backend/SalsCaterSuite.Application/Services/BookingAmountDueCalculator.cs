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

/// <summary>
/// The <b>single</b> net-amount-due path of the product (DEC-042, DEC-046, DEC-099).
/// <para>
/// <b>One rounding (DEC-115).</b> Every published money figure is rounded to <b>two decimals,
/// away from the midpoint</b> (<see cref="MidpointRounding.AwayFromZero"/>), exactly <b>once</b>,
/// in the Application layer. The rounding is not cosmetic: <c>DiscountPercentage</c> is stored
/// as <c>decimal(18,4)</c> while every money column is <c>decimal(18,2)</c>, so a percentage
/// discount that needs a third decimal would leave the stored <c>Booking.AmountDue</c> and the
/// calculator's <c>NetAmountDue</c> half a cent apart. The browser never re-rounds.
/// </para>
/// <para>
/// <b>One source (DEC-099).</b> The <c>Calculate(Booking, decimal)</c> overload <b>reproduces</b>
/// the frozen booking snapshot rather than computing a second, parallel total, so
/// <c>Booking.AmountDue</c> and <c>calculator.NetAmountDue</c> are equal to the cent for every
/// booking and no two surfaces can disagree.
/// </para>
/// </summary>
public class BookingAmountDueCalculator
{
    /// <summary>
    /// The one rounding rule of the money chain (DEC-115, BR-20): two decimals, away from the
    /// midpoint. It is applied to the gross, the discount, the net and the remaining balance.
    /// </summary>
    public static decimal Round2(decimal value) =>
        Math.Round(value, 2, MidpointRounding.AwayFromZero);

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

        // The tax term is zero on this path — the tax policy is UNRESOLVED (DEC-008) — and the
        // two overloads therefore agree whenever TaxAmount is zero (BR-25).
        return CalculateCore(
            basePackageAmount: basePkg,
            extendedAreaAmount: areaCharge,
            addonsAmount: addonsTotal,
            otherChargesAmount: otherChargesTotal,
            discountPercentage: discountPercentage,
            discountFixedAmount: discountFixedAmount,
            taxAmount: 0m,
            totalPaymentsReceived: totalPaymentsReceived);
    }

    /// <summary>
    /// The single authoritative net-amount-due path for a persisted booking (DEC-042, AC-B3).
    /// <para>
    /// Every consumer that needs a booking's net amount due or remaining balance — the payment
    /// ledger, the booking details view, the calendar feed, the customer surfaces and the
    /// operations dashboard — calls this overload, so the argument set exists in exactly one
    /// place and a dashboard-vs-ledger divergence is structurally impossible. The argument set
    /// is deliberately identical to the one the ledger has always used:
    /// </para>
    /// <list type="bullet">
    /// <item><description><c>packageAmount</c> is the <b>frozen stored column</b>
    /// (<c>Booking.PackageAmount</c>), never a recomputed <c>PricePerHead × NumberOfHeads</c>.</description></item>
    /// <item><description><c>discountFixedAmount</c> converts a zero stored discount to
    /// <c>null</c>. The calculator gives the fixed discount precedence and only falls back to the
    /// percentage when the fixed value is <c>null</c> or <c>≤ 0</c>; passing the stored
    /// <c>0</c> directly would silently change which discount rule applies.</description></item>
    /// <item><description>the surcharge is the <b>stored</b> <c>Booking.ExtendedAreaAmount</c>
    /// column (BR-26, DEC-116), not an inference and not the municipality's current rate. The
    /// <c>ApplyExtendedAmount</c> flag is a creation-time switch and an audit marker, so
    /// flipping it after the booking exists can never move the money (AC-BKG-44).</description></item>
    /// </list>
    /// <para>
    /// <paramref name="totalPaymentsReceived"/> must be the authoritative payment sum
    /// (<c>Σ Payment.AmountPaid</c>, DEC-012) — never the denormalized
    /// <c>Booking.AmountPaid</c> cache.
    /// </para>
    /// </summary>
    public BookingCalculationBreakdown Calculate(Booking booking, decimal totalPaymentsReceived)
    {
        return CalculateCore(
            basePackageAmount: booking.PackageAmount,
            extendedAreaAmount: booking.ExtendedAreaAmount,
            addonsAmount: 0m,          // no Booking ↔ AddonDetail link exists (RSK-048)
            otherChargesAmount: 0m,
            discountPercentage: booking.DiscountPercentage,
            discountFixedAmount: booking.DiscountAmount > 0 ? booking.DiscountAmount : null,
            taxAmount: booking.TaxAmount,
            totalPaymentsReceived: totalPaymentsReceived);
    }

    /// <summary>
    /// The one arithmetic of the money chain (BR-25). Both public overloads delegate here, so
    /// the rule exists in exactly one place and the two can never drift apart.
    /// </summary>
    private static BookingCalculationBreakdown CalculateCore(
        decimal basePackageAmount,
        decimal extendedAreaAmount,
        decimal addonsAmount,
        decimal otherChargesAmount,
        decimal discountPercentage,
        decimal? discountFixedAmount,
        decimal taxAmount,
        decimal totalPaymentsReceived)
    {
        decimal gross = Round2(basePackageAmount + extendedAreaAmount + addonsAmount + otherChargesAmount);

        // BR-02: a stored fixed discount takes precedence over the percentage; the percentage
        // is charged on the GROSS amount; the discount is capped at the gross amount.
        decimal rawDiscount = 0m;
        if (discountFixedAmount.HasValue && discountFixedAmount.Value > 0m)
        {
            rawDiscount = discountFixedAmount.Value;
        }
        else if (discountPercentage > 0m)
        {
            rawDiscount = (gross * discountPercentage) / 100m;
        }

        decimal discount = Round2(Math.Min(rawDiscount, gross));

        decimal netDue = Round2(Math.Max(0m, gross - discount + taxAmount));
        decimal balance = Round2(Math.Max(0m, netDue - totalPaymentsReceived));

        bool isFullyPaid = netDue > 0m && totalPaymentsReceived >= netDue;

        return new BookingCalculationBreakdown(
            basePackageAmount,
            extendedAreaAmount,
            addonsAmount,
            otherChargesAmount,
            gross,
            discount,
            netDue,
            totalPaymentsReceived,
            balance,
            isFullyPaid
        );
    }
}
