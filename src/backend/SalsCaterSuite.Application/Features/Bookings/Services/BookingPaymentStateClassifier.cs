using SalsCaterSuite.Domain.Enums;

namespace SalsCaterSuite.Application.Features.Bookings.Services;

/// <summary>
/// The one payment-state vocabulary of the product (BR-28, DEC-177).
/// <para>
/// DEC-177 retired the DEC-117 names. Payment progress is
/// <see cref="PendingPayment"/>, <see cref="DepositPaid"/> and <see cref="FullyPaid"/>, and it
/// is no longer stored in <see cref="BookingStatus"/>. The three states remain mutually
/// exclusive and exhaustive. Zero-due bookings stay fully paid — the former settled treatment.
/// </para>
/// </summary>
public static class BookingPaymentState
{
    /// <summary>Nothing has been collected and something is owed.</summary>
    public const string PendingPayment = nameof(PaymentState.PendingPayment);

    /// <summary>A deposit or any other positive partial collection has been received and a balance remains.</summary>
    public const string DepositPaid = nameof(PaymentState.DepositPaid);

    /// <summary>Nothing is owed, or collected meets or exceeds the net amount due.</summary>
    public const string FullyPaid = nameof(PaymentState.FullyPaid);

    /// <summary>The three states, in the order the classifier evaluates them.</summary>
    public static readonly IReadOnlyList<string> All = new[] { PendingPayment, DepositPaid, FullyPaid };

    /// <summary>The server-provided display label of a payment state.</summary>
    public static string Label(string state) => state switch
    {
        PendingPayment => "Pending payment",
        DepositPaid => "Deposit paid",
        FullyPaid => "Fully paid",
        _ => throw new ArgumentOutOfRangeException(
            nameof(state), state, "Unknown booking payment state.")
    };
}

/// <summary>
/// The <b>single</b> classifier of the payment state and of the overdue dimension (BR-28,
/// DEC-041, DEC-117). The register, the summary and the quote all call it, so a booking can
/// never be reported as two different payment states on two different screens.
/// <para>
/// The evaluation order is <c>PendingPayment → DepositPaid → FullyPaid</c> and the three are
/// exclusive by construction: <c>Collected == 0</c> and <c>Collected &gt; 0</c> cannot both
/// hold, and <c>Collected &lt; NetAmountDue</c> and <c>Collected ≥ NetAmountDue</c> cannot both
/// hold. Every booking therefore has exactly one state and the three counts sum to the
/// population's booking count.
/// </para>
/// <para>
/// <b>Overdue is a separate dimension</b>, never a fourth state: a partly-paid booking is
/// <c>DepositPaid</c> <i>and</i> overdue, and folding the two into one vocabulary would make
/// the states overlap (BR-28).
/// </para>
/// </summary>
public static class BookingPaymentStateClassifier
{
    /// <summary>
    /// The payment state of a booking.
    /// <list type="bullet">
    /// <item><description><c>PendingPayment</c> — <c>Collected == 0</c> and
    /// <c>NetAmountDue &gt; 0</c>.</description></item>
    /// <item><description><c>DepositPaid</c> — <c>Collected &gt; 0</c> and
    /// <c>Collected &lt; NetAmountDue</c>. The first payment counts as the deposit.</description></item>
    /// <item><description><c>FullyPaid</c> — <c>Collected ≥ NetAmountDue</c>. This deliberately
    /// covers the zero-value booking (<c>NetAmountDue == 0</c>, nothing collected — the former
    /// settled treatment) and the overpayment (<c>Collected &gt; NetAmountDue</c>).</description></item>
    /// </list>
    /// </summary>
    public static string Classify(decimal collected, decimal netAmountDue)
    {
        return PaymentStateRules.Classify(collected, netAmountDue).ToString();
    }

    /// <summary>
    /// Whether a payment-state name is one of the three published states. Used by the register's
    /// filter, which rejects an unknown name with <c>400</c> rather than widening the
    /// vocabulary silently (BR-32).
    /// </summary>
    public static bool IsKnown(string? state) =>
        state is not null && BookingPaymentState.All.Contains(state, StringComparer.Ordinal);

    /// <summary>
    /// Whether a booking is <b>overdue</b>: it carries a balance and its event date — the due
    /// date (DEC-041) — has passed.
    /// </summary>
    public static bool IsOverdue(decimal outstanding, DateOnly eventDate, DateOnly today) =>
        outstanding > 0m && eventDate < today;

    /// <summary>
    /// The number of days until the event, or <c>null</c> when the event is not in the future.
    /// The server's clock owns the figure; the browser never derives it (contract §14.1).
    /// </summary>
    public static int? DaysUntilEvent(DateOnly eventDate, DateOnly today)
    {
        var days = eventDate.DayNumber - today.DayNumber;
        return days > 0 ? days : null;
    }
}
