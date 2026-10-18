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
}

