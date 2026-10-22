namespace SalsCaterSuite.UnitTests.Application.Services;

using FluentAssertions;
using SalsCaterSuite.Application.Features.Bookings.Services;
using Xunit;

/// <summary>
/// The payment-state classifier and the overdue dimension (BR-28, DEC-041, DEC-117).
/// <para>
/// The three states are mutually exclusive and exhaustive, so every booking has exactly one
/// state and the three counts sum to the population's booking count.
/// </para>
/// </summary>
public class BookingPaymentStateClassifierTests
{
    [Fact]
    public void Classify_should_return_exactly_one_state()
    {
        // Nothing collected and nothing owed — a zero-value booking.
        BookingPaymentStateClassifier.Classify(0m, 0m).Should().Be(BookingPaymentState.Settled);

        // Nothing collected and something owed.
        BookingPaymentStateClassifier.Classify(0m, 100m).Should().Be(BookingPaymentState.AwaitingDeposit);

        // A deposit received and a balance remaining.
        BookingPaymentStateClassifier.Classify(50m, 100m).Should().Be(BookingPaymentState.PartiallyPaid);

        // Paid exactly.
        BookingPaymentStateClassifier.Classify(100m, 100m).Should().Be(BookingPaymentState.Settled);

        // Overpaid.
        BookingPaymentStateClassifier.Classify(150m, 100m).Should().Be(BookingPaymentState.Settled);
    }

    [Fact]
    public void The_zero_value_booking_should_be_settled_and_never_awaiting_a_deposit()
    {
        BookingPaymentStateClassifier.Classify(0m, 0m).Should().Be(BookingPaymentState.Settled);
    }

    [Fact]
    public void The_overpaid_booking_should_be_settled_and_never_awaiting_a_deposit()
    {
        BookingPaymentStateClassifier.Classify(150m, 100m).Should().Be(BookingPaymentState.Settled);
    }

    [Fact]
    public void The_three_states_should_be_exhaustive_for_every_combination()
    {
        var states = new List<string>();

        for (var collected = 0m; collected <= 200m; collected += 25m)
        {
            for (var net = 0m; net <= 200m; net += 25m)
            {
                var state = BookingPaymentStateClassifier.Classify(collected, net);

                state.Should().BeOneOf(BookingPaymentState.All);
                states.Add(state);
            }
        }

        states.Should().OnlyContain(state => BookingPaymentState.All.Contains(state));
    }

    [Fact]
    public void The_payment_state_names_should_never_collide_with_the_lifecycle_names()
    {
        // DEC-117: DepositPaid and FullyPaid are LIFECYCLE names; the payment states are
        // AwaitingDeposit, PartiallyPaid and Settled.
        BookingPaymentState.All.Should().NotContain("DepositPaid");
        BookingPaymentState.All.Should().NotContain("FullyPaid");
        BookingPaymentState.All.Should().Contain("AwaitingDeposit");
        BookingPaymentState.All.Should().Contain("PartiallyPaid");
        BookingPaymentState.All.Should().Contain("Settled");
    }

    [Fact]
    public void IsOverdue_should_be_true_only_for_an_outstanding_balance_past_the_event_date()
    {
        var today = new DateOnly(2026, 10, 22);

        BookingPaymentStateClassifier.IsOverdue(1_000m, new DateOnly(2026, 10, 21), today)
            .Should().BeTrue("the event date is the due date (DEC-041)");
        BookingPaymentStateClassifier.IsOverdue(1_000m, new DateOnly(2026, 10, 22), today)
            .Should().BeFalse("today is not yet past");
        BookingPaymentStateClassifier.IsOverdue(1_000m, new DateOnly(2026, 10, 23), today)
            .Should().BeFalse("the event is in the future");
        BookingPaymentStateClassifier.IsOverdue(0m, new DateOnly(2026, 10, 21), today)
            .Should().BeFalse("nothing is owed");
    }

    [Fact]
    public void DaysUntilEvent_should_be_null_when_the_event_is_not_in_the_future()
    {
        var today = new DateOnly(2026, 10, 22);

        BookingPaymentStateClassifier.DaysUntilEvent(new DateOnly(2026, 10, 25), today).Should().Be(3);
        BookingPaymentStateClassifier.DaysUntilEvent(new DateOnly(2026, 10, 22), today).Should().BeNull();
        BookingPaymentStateClassifier.DaysUntilEvent(new DateOnly(2026, 10, 21), today).Should().BeNull();
    }

    [Fact]
    public void IsKnown_should_accept_only_the_three_published_states()
    {
        BookingPaymentStateClassifier.IsKnown(BookingPaymentState.AwaitingDeposit).Should().BeTrue();
        BookingPaymentStateClassifier.IsKnown(BookingPaymentState.PartiallyPaid).Should().BeTrue();
        BookingPaymentStateClassifier.IsKnown(BookingPaymentState.Settled).Should().BeTrue();
        BookingPaymentStateClassifier.IsKnown("DepositPaid").Should().BeFalse();
        BookingPaymentStateClassifier.IsKnown("FullyPaid").Should().BeFalse();
        BookingPaymentStateClassifier.IsKnown(null).Should().BeFalse();
    }
}
