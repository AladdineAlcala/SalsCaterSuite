namespace SalsCaterSuite.UnitTests.Application.Services;

using FluentAssertions;
using SalsCaterSuite.Application.Features.Bookings.Services;
using Xunit;

/// <summary>
/// The menu-change window (BR-36, DEC-128).
/// <para>
/// <c>LockDate = EventDate − LockDays</c>. The <b>date</b> is always concrete; only the
/// <b>days</b> figure is <c>null</c> when the configuration key is absent. The write is refused
/// once <c>today &gt;= lockDate</c>.
/// </para>
/// </summary>
public class BookingMenuWindowTests
{
    private static readonly DateOnly EventDate = new(2026, 12, 20);

    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    [InlineData(-3)]
    public void An_absent_zero_or_negative_configuration_should_lock_on_the_event_date(int? lockDays)
    {
        var window = BookingMenuWindow.Resolve(EventDate, lockDays, new DateOnly(2026, 12, 1));

        window.LockDate.Should().Be(EventDate);
        window.LockDays.Should().Be(lockDays);
        window.IsLocked.Should().BeFalse("the event has not begun");
    }

    [Fact]
    public void The_lock_date_should_be_the_event_date_less_the_configured_days()
    {
        var window = BookingMenuWindow.Resolve(EventDate, 7, new DateOnly(2026, 12, 1));

        window.LockDate.Should().Be(new DateOnly(2026, 12, 13));
        window.LockDays.Should().Be(7);
        window.IsLocked.Should().BeFalse();
    }

    /// <summary>
    /// The lock is inclusive of its own date: on the lock date the window has <b>closed</b>, so
    /// the boundary is <c>today &gt;= lockDate</c> rather than <c>&gt;</c>.
    /// </summary>
    [Fact]
    public void The_day_before_the_lock_date_should_still_be_editable()
    {
        BookingMenuWindow.Resolve(EventDate, 7, new DateOnly(2026, 12, 12)).IsLocked
            .Should().BeFalse();
    }

    [Fact]
    public void The_lock_date_itself_should_be_locked()
    {
        BookingMenuWindow.Resolve(EventDate, 7, new DateOnly(2026, 12, 13)).IsLocked
            .Should().BeTrue();
    }

    [Fact]
    public void A_date_after_the_lock_date_should_be_locked()
    {
        BookingMenuWindow.Resolve(EventDate, 7, new DateOnly(2026, 12, 19)).IsLocked
            .Should().BeTrue();
    }

    [Fact]
    public void An_absent_configuration_should_lock_on_the_event_date_itself()
    {
        BookingMenuWindow.Resolve(EventDate, null, EventDate).IsLocked.Should().BeTrue();
    }

    /// <summary>
    /// A lock longer than the lead time puts the lock date in the past, so the window is already
    /// closed — the arithmetic is applied literally rather than clamped.
    /// </summary>
    [Fact]
    public void A_lock_that_begins_before_today_should_already_be_closed()
    {
        var window = BookingMenuWindow.Resolve(EventDate, 30, new DateOnly(2026, 12, 1));

        window.LockDate.Should().Be(new DateOnly(2026, 11, 20));
        window.LockDays.Should().Be(30);
        window.IsLocked.Should().BeTrue();
    }
}
