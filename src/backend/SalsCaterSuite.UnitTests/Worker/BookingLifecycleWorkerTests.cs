namespace SalsCaterSuite.UnitTests.Worker;

using System.Reflection;
using FluentAssertions;
using SalsCaterSuite.Domain.Entities;
using SalsCaterSuite.Domain.Enums;
using SalsCaterSuite.Worker.BackgroundServices;
using Xunit;

public class BookingLifecycleWorkerTests
{
    private static Booking CreateConfirmedBooking(DateOnly eventDate, TimeSpan? packoutTime = null)
    {
        var booking = Booking.Create(
            customerId: 1,
            noOfPerson: 30,
            bookType: BookingType.Inside,
            typeOfService: ServiceType.Catering,
            eventDate: eventDate,
            startDate: eventDate.ToDateTime(new TimeOnly(9, 0), DateTimeKind.Utc),
            endDate: eventDate.ToDateTime(new TimeOnly(22, 0), DateTimeKind.Utc),
            occasion: "Lifecycle test",
            packageName: "Test Package",
            pricePerHead: 100m,
            callTime: new TimeSpan(8, 0, 0),
            packoutTime: packoutTime);

        booking.Confirm();
        return booking;
    }

    private static Booking CreateInProgressBooking(DateOnly eventDate, TimeSpan packoutTime)
    {
        var booking = CreateConfirmedBooking(eventDate, packoutTime);
        booking.MarkInProgress();
        return booking;
    }

    private static Booking CreateBookingWithStatus(BookingStatus status, DateOnly eventDate, TimeSpan packoutTime)
    {
        var booking = CreateConfirmedBooking(eventDate, packoutTime);
        typeof(Booking)
            .GetProperty(nameof(Booking.Status), BindingFlags.Instance | BindingFlags.Public)!
            .SetValue(booking, status);
        return booking;
    }

    [Fact]
    public void ShouldAdvanceToInProgress_WhenConfirmedOnEventDate()
    {
        var booking = CreateConfirmedBooking(DateOnly.FromDateTime(DateTime.UtcNow));

        var result = BookingLifecycleWorker.ShouldAdvanceToInProgress(booking, DateTime.UtcNow);

        result.Should().BeTrue();
    }

    [Fact]
    public void ShouldNotAdvanceToInProgress_WhenConfirmedBeforeEventDate()
    {
        var futureDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1));
        var booking = CreateConfirmedBooking(futureDate);

        var result = BookingLifecycleWorker.ShouldAdvanceToInProgress(booking, DateTime.UtcNow);

        result.Should().BeFalse();
    }

    [Fact]
    public void ShouldAutoComplete_WhenInProgressAndPackoutGraceWindowHasPassed()
    {
        var eventDate = DateOnly.FromDateTime(DateTime.UtcNow);
        var packoutTime = new TimeSpan(18, 0, 0);
        var booking = CreateInProgressBooking(eventDate, packoutTime);
        var now = eventDate.ToDateTime(new TimeOnly(19, 30), DateTimeKind.Utc);

        var result = BookingLifecycleWorker.ShouldAutoComplete(booking, now);

        result.Should().BeTrue();
    }

    [Fact]
    public void ShouldNotAutoComplete_WhenBookingIsCancelledRescheduledOrNoShow()
    {
        var eventDate = DateOnly.FromDateTime(DateTime.UtcNow);
        var packoutTime = new TimeSpan(18, 0, 0);
        var now = eventDate.ToDateTime(new TimeOnly(20, 0), DateTimeKind.Utc);

        var cancelled = CreateBookingWithStatus(BookingStatus.Cancelled, eventDate, packoutTime);
        var rescheduled = CreateBookingWithStatus(BookingStatus.Rescheduled, eventDate, packoutTime);
        var noShow = CreateBookingWithStatus(BookingStatus.NoShow, eventDate, packoutTime);

        BookingLifecycleWorker.ShouldAutoComplete(cancelled, now).Should().BeFalse();
        BookingLifecycleWorker.ShouldAutoComplete(rescheduled, now).Should().BeFalse();
        BookingLifecycleWorker.ShouldAutoComplete(noShow, now).Should().BeFalse();
    }

    [Fact]
    public void MarkCompleted_ShouldRejectCancelledNoShowAndRescheduledBookings()
    {
        var eventDate = DateOnly.FromDateTime(DateTime.UtcNow);
        var cancelled = CreateBookingWithStatus(BookingStatus.Cancelled, eventDate, new TimeSpan(18, 0, 0));
        var rescheduled = CreateBookingWithStatus(BookingStatus.Rescheduled, eventDate, new TimeSpan(18, 0, 0));
        var noShow = CreateBookingWithStatus(BookingStatus.NoShow, eventDate, new TimeSpan(18, 0, 0));

        cancelled.Invoking(x => x.MarkCompleted()).Should().Throw<Exception>();
        rescheduled.Invoking(x => x.MarkCompleted()).Should().Throw<Exception>();
        noShow.Invoking(x => x.MarkCompleted()).Should().Throw<Exception>();
    }
}
