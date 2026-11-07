using SalsCaterSuite.Domain.Exceptions;

namespace SalsCaterSuite.Domain.Enums;

/// <summary>
/// Payment progress of a booking (DEC-177). This is not a lifecycle status.
/// </summary>
public enum PaymentState
{
    /// <summary>Nothing has been collected and the net amount due is positive.</summary>
    PendingPayment = 0,

    /// <summary>A positive partial collection. The first payment counts as the deposit.</summary>
    DepositPaid = 1,

    /// <summary>
    /// Collected meets or exceeds the net amount due. A zero-due booking is fully paid
    /// even when nothing was collected — the former settled treatment (DEC-177).
    /// </summary>
    FullyPaid = 2
}

/// <summary>
/// The one derivation of <see cref="PaymentState"/> (DEC-177). Lifecycle status is never an input.
/// </summary>
public static class PaymentStateRules
{
    /// <summary>
    /// Classifies payment progress from the authoritative non-soft-deleted payment sum and the
    /// net amount due. Overpayment and a zero due are <see cref="PaymentState.FullyPaid"/>.
    /// </summary>
    public static PaymentState Classify(decimal collected, decimal netAmountDue)
    {
        if (collected < 0m)
        {
            throw new DomainException("The collected amount cannot be negative.");
        }

        if (collected == 0m && netAmountDue > 0m)
        {
            return PaymentState.PendingPayment;
        }

        if (collected > 0m && collected < netAmountDue)
        {
            return PaymentState.DepositPaid;
        }

        return PaymentState.FullyPaid;
    }
}
