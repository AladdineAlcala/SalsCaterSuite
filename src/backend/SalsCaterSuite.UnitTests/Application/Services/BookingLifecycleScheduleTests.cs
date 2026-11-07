namespace SalsCaterSuite.UnitTests.Application.Services;

using FluentAssertions;
using SalsCaterSuite.Application.Common.Interfaces;
using SalsCaterSuite.Application.Features.Bookings.Services;
using SalsCaterSuite.Domain.Entities;
using SalsCaterSuite.Domain.Enums;
using Xunit;

/// <summary>
/// BG-04 schedule and the payment/lifecycle split (DEC-177, DEC-178).
/// Instants are Asia/Manila wall times converted to UTC. The offset is asserted so a
/// machine-local clock cannot hide a boundary error.
/// </summary>
public class BookingLifecycleScheduleTests
{
    private static readonly DateOnly EventDate = new(2026, 10, 28);
    private static readonly TimeSpan Start = new(18, 0, 0);
    private static readonly TimeSpan End = new(22, 0, 0);

    [Fact]
    public void Manila_has_a_fixed_eight_hour_offset()
    {
        var offset = BookingLifecycleSchedule.Manila.GetUtcOffset(
            new DateTimeOffset(2026, 10, 28, 10, 0, 0, TimeSpan.Zero));

        offset.Should().Be(TimeSpan.FromHours(8));
    }

    [Fact]
    public void Confirmed_advances_at_the_Manila_start_instant_and_not_one_second_before()
    {
        var start = BookingLifecycleSchedule.ToUtcInstant(EventDate, Start);

        Due(BookingStatus.Confirmed, start.AddSeconds(-1)).Should().BeEmpty();

        var due = Due(BookingStatus.Confirmed, start);
        due.Should().ContainSingle();
        due[0].From.Should().Be(BookingStatus.Confirmed);
        due[0].To.Should().Be(BookingStatus.InProgress);
        due[0].EffectiveAtUtc.Should().Be(start);
    }

    [Fact]
    public void An_overnight_event_completes_from_the_end_date_not_the_start_date()
    {
        var endDate = EventDate.AddDays(1);
        var overnightEnd = new TimeSpan(2, 0, 0);
        var completion = BookingLifecycleSchedule.CompletionThresholdUtc(endDate, packoutTime: null, overnightEnd);
        var start = BookingLifecycleSchedule.ToUtcInstant(EventDate, new TimeSpan(22, 0, 0));

        completion.Should().Be(BookingLifecycleSchedule.ToUtcInstant(endDate, new TimeSpan(3, 0, 0)));
        completion.Should().BeAfter(start);

        var atStart = Due(
            BookingStatus.Confirmed,
            start,
            eventEndDate: endDate,
            eventStartTime: new TimeSpan(22, 0, 0),
            eventEndTime: overnightEnd);
        atStart.Should().ContainSingle()
            .Which.To.Should().Be(BookingStatus.InProgress);

        var pastBoth = Due(
            BookingStatus.Confirmed,
            completion,
            eventEndDate: endDate,
            eventStartTime: new TimeSpan(22, 0, 0),
            eventEndTime: overnightEnd);
        pastBoth.Select(transition => transition.To).Should().Equal(
            BookingStatus.InProgress,
            BookingStatus.Completed);
    }

    [Fact]
    public void A_null_packout_falls_back_to_event_end_plus_one_hour()
    {
        var completion = BookingLifecycleSchedule.CompletionThresholdUtc(EventDate, packoutTime: null, End);

        completion.Should().Be(BookingLifecycleSchedule.ToUtcInstant(EventDate, End).AddHours(1));

        Due(BookingStatus.InProgress, completion.AddSeconds(-1), packoutTime: null)
            .Should().BeEmpty();
        Due(BookingStatus.InProgress, completion, packoutTime: null)
            .Should().ContainSingle()
            .Which.To.Should().Be(BookingStatus.Completed);
    }

    [Fact]
    public void A_late_packout_rolls_completion_into_the_next_Manila_day()
    {
        var packout = new TimeSpan(23, 30, 0);
        var completion = BookingLifecycleSchedule.CompletionThresholdUtc(EventDate, packout, End);

        completion.Should().Be(BookingLifecycleSchedule.ToUtcInstant(EventDate.AddDays(1), new TimeSpan(0, 30, 0)));
    }

    [Theory]
    [InlineData(BookingStatus.Pending)]
    [InlineData(BookingStatus.Expired)]
    [InlineData(BookingStatus.Cancelled)]
    [InlineData(BookingStatus.Rescheduled)]
    [InlineData(BookingStatus.NoShow)]
    [InlineData(BookingStatus.Completed)]
    public void Excluded_statuses_are_never_due(BookingStatus status)
    {
        var farFuture = BookingLifecycleSchedule.ToUtcInstant(EventDate, Start).AddDays(3);

        Due(status, farFuture).Should().BeEmpty();
    }

    [Fact]
    public void A_soft_deleted_confirmed_booking_is_not_due()
    {
        var start = BookingLifecycleSchedule.ToUtcInstant(EventDate, Start);

        Due(BookingStatus.Confirmed, start.AddDays(1), isDeleted: true).Should().BeEmpty();
    }

    [Fact]
    public void Repeating_the_schedule_is_idempotent_and_an_in_progress_booking_is_not_restarted()
    {
        var start = BookingLifecycleSchedule.ToUtcInstant(EventDate, Start);
        var first = Due(BookingStatus.Confirmed, start);
        var second = Due(BookingStatus.Confirmed, start);

        second.Should().Equal(first);
        Due(BookingStatus.InProgress, start).Should().BeEmpty();
    }

    [Fact]
    public void A_payment_changes_payment_state_and_never_the_lifecycle_status()
    {
        var booking = CreateBooking(pricePerHead: 0m);
        booking.Status.Should().Be(BookingStatus.Pending);
        booking.PaymentState.Should().Be(PaymentState.FullyPaid, "a zero due is fully paid");

        booking = CreateBooking(pricePerHead: 100m);
        booking.RecalculateFinancials(0m, 0m, 0m, 0m);
        booking.RefreshPaymentStatus(0m, booking.AmountDue);
        booking.Status.Should().Be(BookingStatus.Pending);
        booking.PaymentState.Should().Be(PaymentState.PendingPayment);

        booking.RecordPayment(400m);
        booking.RefreshPaymentStatus(400m, 1_000m);
        booking.Status.Should().Be(BookingStatus.Pending);
        booking.PaymentState.Should().Be(PaymentState.DepositPaid);

        booking.RecordPayment(600m);
        booking.RefreshPaymentStatus(1_000m, 1_000m);
        booking.Status.Should().Be(BookingStatus.Pending);
        booking.PaymentState.Should().Be(PaymentState.FullyPaid);
    }

    [Fact]
    public void A_scheduled_transition_does_not_change_payment_state_or_amounts()
    {
        var booking = CreateBooking(pricePerHead: 100m);
        booking.Confirm();
        booking.RecordPayment(100m);
        booking.RefreshPaymentStatus(100m, 1_000m);
        var paid = booking.AmountPaid;
        var due = booking.AmountDue;

        var applied = booking.TryApplyScheduledTransition(BookingStatus.Confirmed, BookingStatus.InProgress);

        applied.Should().BeTrue();
        booking.Status.Should().Be(BookingStatus.InProgress);
        booking.PaymentState.Should().Be(PaymentState.DepositPaid);
        booking.AmountPaid.Should().Be(paid);
        booking.AmountDue.Should().Be(due);
        booking.TryApplyScheduledTransition(BookingStatus.Confirmed, BookingStatus.InProgress).Should().BeFalse();
    }

    private static IReadOnlyList<BookingLifecycleTransition> Due(
        BookingStatus status,
        DateTime utcNow,
        DateOnly? eventEndDate = null,
        TimeSpan? eventStartTime = null,
        TimeSpan? eventEndTime = null,
        TimeSpan? packoutTime = null,
        bool isDeleted = false) =>
        BookingLifecycleSchedule.DueTransitions(
            status,
            isDeleted,
            EventDate,
            eventStartTime ?? Start,
            eventEndDate ?? EventDate,
            packoutTime,
            eventEndTime ?? End,
            utcNow);

    private static Booking CreateBooking(decimal pricePerHead)
    {
        var start = EventDate.ToDateTime(TimeOnly.FromTimeSpan(Start));
        return Booking.Create(
            customerId: 1,
            noOfPerson: 10,
            bookType: BookingType.Inside,
            typeOfService: ServiceType.Catering,
            eventDate: EventDate,
            startDate: start,
            endDate: start.AddHours(4),
            pricePerHead: pricePerHead);
    }
}

/// <summary>The batch processor records history in order and treats a lost row version as a skip.</summary>
public class BookingLifecycleTransitionServiceTests
{
    [Fact]
    public async Task A_confirmed_booking_past_both_thresholds_records_both_transitions_in_one_commit()
    {
        var booking = ConfirmedBooking();
        var now = BookingLifecycleSchedule.CompletionThresholdUtc(
            booking.EventEndDate, booking.PackoutTime, booking.EventEndTime);
        var repository = new FakeLifecycleRepository(booking);
        var service = new BookingLifecycleTransitionService(repository, new FixedClock(now));

        var result = await service.ProcessDueBatchAsync(10);

        result.Committed.Should().Be(1);
        booking.Status.Should().Be(BookingStatus.Completed);
        repository.History.Select(row => row.StatusName).Should().Equal("InProgress", "Completed");
        repository.History.Should().OnlyContain(row =>
            row.ActorSource == BookingStatusHistory.Bg04ActorSource
            && row.RecordedAtUtc == now);
        repository.Commits.Should().Be(1);
        booking.PaymentState.Should().Be(PaymentState.PendingPayment);
    }

    [Fact]
    public async Task A_lost_row_version_commits_nothing_and_is_not_counted()
    {
        var booking = ConfirmedBooking();
        var now = BookingLifecycleSchedule.ToUtcInstant(booking.EventDate, booking.EventStartTime);
        var repository = new FakeLifecycleRepository(booking) { CommitSucceeds = false };
        var service = new BookingLifecycleTransitionService(repository, new FixedClock(now));

        var result = await service.ProcessDueBatchAsync(10);

        result.Committed.Should().Be(0);
        result.Selected.Should().Be(1);
        repository.Commits.Should().Be(1);
    }

    [Fact]
    public async Task A_booking_that_is_no_longer_due_when_loaded_is_skipped()
    {
        var booking = ConfirmedBooking();
        booking.Cancel();
        var now = BookingLifecycleSchedule.ToUtcInstant(booking.EventDate, booking.EventStartTime).AddDays(2);
        var repository = new FakeLifecycleRepository(booking);
        var service = new BookingLifecycleTransitionService(repository, new FixedClock(now));

        var result = await service.ProcessDueBatchAsync(10);

        result.Committed.Should().Be(0);
        repository.Commits.Should().Be(0);
        repository.History.Should().BeEmpty();
        booking.Status.Should().Be(BookingStatus.Cancelled);
    }

    private static Booking ConfirmedBooking()
    {
        var date = new DateOnly(2026, 10, 28);
        var start = date.ToDateTime(new TimeOnly(18, 0));
        var booking = Booking.Create(
            customerId: 1,
            noOfPerson: 10,
            bookType: BookingType.Inside,
            typeOfService: ServiceType.Catering,
            eventDate: date,
            startDate: start,
            endDate: start.AddHours(4),
            pricePerHead: 100m);
        booking.Confirm();
        booking.RefreshPaymentStatus(0m, 1_000m);
        return booking;
    }

    private sealed class FixedClock(DateTime utcNow) : IDateTime
    {
        public DateTime Now => utcNow;
        public DateTime UtcNow => utcNow;
    }

    private sealed class FakeLifecycleRepository : IBookingLifecycleRepository
    {
        private readonly Booking _booking;

        public FakeLifecycleRepository(Booking booking) => _booking = booking;

        public List<BookingStatusHistory> History { get; } = new();

        public int Commits { get; private set; }

        public bool CommitSucceeds { get; set; } = true;

        public Task<IReadOnlyList<int>> GetDueIdsAsync(
            DateTime utcNow,
            int batchSize,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<int>>(new[] { 1 });

        public Task<Booking?> GetTrackedAsync(int transactionId, CancellationToken cancellationToken = default) =>
            Task.FromResult<Booking?>(_booking);

        public void StageHistory(BookingStatusHistory history) => History.Add(history);

        public Task<bool> TryCommitAsync(CancellationToken cancellationToken = default)
        {
            Commits++;
            return Task.FromResult(CommitSucceeds);
        }
    }
}
