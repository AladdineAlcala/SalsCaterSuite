using SalsCaterSuite.Domain.Enums;

namespace SalsCaterSuite.Application.Features.Bookings.Services;

/// <summary>
/// The one payment-state vocabulary of the product (BR-28, DEC-117).
/// <para>
/// The payment state answers one question — <i>how far along is the payment?</i> — and it is a
/// <b>three-state</b>, mutually exclusive and exhaustive vocabulary. The names deliberately do
/// <b>not</b> collide with the <see cref="BookingStatus"/> lifecycle vocabulary: contract
/// section 5 publishes <c>DepositPaid</c> and <c>FullyPaid</c> as <i>lifecycle</i> names, so the
/// payment states are named <see cref="AwaitingDeposit"/>, <see cref="PartiallyPaid"/> and
/// <see cref="Settled"/>. A payload that carries both vocabularies can therefore be read
/// unambiguously (AC-BKG-45).
/// </para>
/// </summary>
public static class BookingPaymentState
{
    /// <summary>Nothing has been collected and something is owed.</summary>
    public const string AwaitingDeposit = "AwaitingDeposit";

    /// <summary>A deposit or a partial payment has been received and a balance remains.</summary>
    public const string PartiallyPaid = "PartiallyPaid";

    /// <summary>Nothing is owed. This covers the zero-value booking and the overpayment.</summary>
    public const string Settled = "Settled";

    /// <summary>The three states, in the order the classifier evaluates them.</summary>
    public static readonly IReadOnlyList<string> All = new[] { AwaitingDeposit, PartiallyPaid, Settled };

    /// <summary>The server-provided display label of a payment state.</summary>
    public static string Label(string state) => state switch
    {
        AwaitingDeposit => "Awaiting initial deposit",
        PartiallyPaid => "Partially paid",
        Settled => "Fully paid",
        _ => throw new ArgumentOutOfRangeException(
            nameof(state), state, "Unknown booking payment state.")
    };
}

/// <summary>
/// The <b>single</b> classifier of the payment state and of the overdue dimension (BR-28,
/// DEC-041, DEC-117). The register, the summary and the quote all call it, so a booking can
/// never be reported as two different payment states on two different screens.
/// <para>
/// The evaluation order is <c>AwaitingDeposit → PartiallyPaid → Settled</c> and the three are
/// exclusive by construction: <c>Collected == 0</c> and <c>Collected &gt; 0</c> cannot both
/// hold, and <c>Collected &lt; NetAmountDue</c> and <c>Collected ≥ NetAmountDue</c> cannot both
/// hold. Every booking therefore has exactly one state and the three counts sum to the
/// population's booking count.
/// </para>
/// <para>
/// <b>Overdue is a separate dimension</b>, never a fourth state: a partly-paid booking is
/// <c>PartiallyPaid</c> <i>and</i> overdue, and folding the two into one vocabulary would make
/// the states overlap (BR-28).
/// </para>
/// </summary>
public static class BookingPaymentStateClassifier
{
    /// <summary>
    /// The payment state of a booking.
    /// <list type="bullet">
    /// <item><description><c>AwaitingDeposit</c> — <c>Collected == 0</c> and
    /// <c>NetAmountDue &gt; 0</c>.</description></item>
    /// <item><description><c>PartiallyPaid</c> — <c>Collected &gt; 0</c> and
    /// <c>Collected &lt; NetAmountDue</c>.</description></item>
    /// <item><description><c>Settled</c> — <c>Collected ≥ NetAmountDue</c>. This deliberately
    /// covers the zero-value booking (<c>NetAmountDue == 0</c>, nothing collected) and the
    /// overpayment (<c>Collected &gt; NetAmountDue</c>); neither may be reported as awaiting
    /// anything.</description></item>
    /// </list>
    /// </summary>
    public static string Classify(decimal collected, decimal netAmountDue)
    {
        if (collected == 0m && netAmountDue > 0m)
        {
            return BookingPaymentState.AwaitingDeposit;
        }

        if (collected > 0m && collected < netAmountDue)
        {
            return BookingPaymentState.PartiallyPaid;
        }

        return BookingPaymentState.Settled;
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
