namespace SalsCaterSuite.UnitTests.Worker;

using System.Reflection;
using FluentAssertions;
using SalsCaterSuite.Domain.Entities;
using SalsCaterSuite.Domain.Enums;
using SalsCaterSuite.Worker.BackgroundServices;
using Xunit;

/// <summary>
/// The old date-only worker predicates are gone (DEC-178). This file keeps the domain
/// rejection of completing an excluded lifecycle status, and proves the retained worker
/// type no longer exposes the old transition predicates.
/// </summary>

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
    public void The_retained_worker_should_not_expose_the_retired_date_only_predicates()
    {
        typeof(BookingLifecycleWorker).GetMethod("ShouldAdvanceToInProgress").Should().BeNull();
        typeof(BookingLifecycleWorker).GetMethod("ShouldAutoComplete").Should().BeNull();
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
