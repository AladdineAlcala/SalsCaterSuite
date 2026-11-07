using SalsCaterSuite.Domain.Enums;

namespace SalsCaterSuite.Application.Features.Bookings.Services;

/// <summary>One approved BG-04 transition, with the instant it became effective (UTC).</summary>
public readonly record struct BookingLifecycleTransition(
    BookingStatus From,
    BookingStatus To,
    DateTime EffectiveAtUtc);

/// <summary>
/// The pure schedule of BG-04 (DEC-178). Scheduled date/time values are interpreted in
/// Asia/Manila and compared as UTC instants. This type does not read a clock or a database.
/// </summary>
public static class BookingLifecycleSchedule
{
    public const string ManilaTimeZoneId = "Asia/Manila";

    public static readonly TimeZoneInfo Manila = TimeZoneInfo.FindSystemTimeZoneById(ManilaTimeZoneId);

    /// <summary>The Manila wall-clock date and time, converted to a UTC instant.</summary>
    public static DateTime ToUtcInstant(DateOnly date, TimeSpan time)
    {
        var unspecified = date.ToDateTime(TimeOnly.FromTimeSpan(time), DateTimeKind.Unspecified);
        return TimeZoneInfo.ConvertTimeToUtc(unspecified, Manila);
    }

    /// <summary>
    /// Pack-out plus one hour when pack-out is stated; otherwise event end plus one hour.
    /// The hour is added in Manila local time, so a 23:30 pack-out completes at 00:30 the next day.
    /// </summary>
    public static DateTime CompletionThresholdUtc(
        DateOnly eventEndDate,
        TimeSpan? packoutTime,
        TimeSpan eventEndTime)
    {
        var clock = packoutTime ?? eventEndTime;
        var local = eventEndDate
            .ToDateTime(TimeOnly.FromTimeSpan(clock), DateTimeKind.Unspecified)
            .AddHours(1);
        return TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), Manila);
    }

    /// <summary>
    /// The transitions that are due at <paramref name="utcNow"/>, in order.
    /// A confirmed booking that is already past both thresholds yields both transitions.
    /// Excluded lifecycle statuses and soft-deleted rows yield none.
    /// </summary>
    public static IReadOnlyList<BookingLifecycleTransition> DueTransitions(
        BookingStatus status,
        bool isDeleted,
        DateOnly eventDate,
        TimeSpan eventStartTime,
        DateOnly eventEndDate,
        TimeSpan? packoutTime,
        TimeSpan eventEndTime,
        DateTime utcNow)
    {
        if (isDeleted)
        {
            return [];
        }

        var now = DateTime.SpecifyKind(utcNow, DateTimeKind.Utc);
        var start = ToUtcInstant(eventDate, eventStartTime);
        var completion = CompletionThresholdUtc(eventEndDate, packoutTime, eventEndTime);
        var due = new List<BookingLifecycleTransition>(2);

        if (status == BookingStatus.Confirmed && now >= start)
        {
            due.Add(new BookingLifecycleTransition(BookingStatus.Confirmed, BookingStatus.InProgress, start));
            if (now >= completion)
            {
                due.Add(new BookingLifecycleTransition(BookingStatus.InProgress, BookingStatus.Completed, completion));
            }
        }
        else if (status == BookingStatus.InProgress && now >= completion)
        {
            due.Add(new BookingLifecycleTransition(BookingStatus.InProgress, BookingStatus.Completed, completion));
        }

        return due;
    }
}
