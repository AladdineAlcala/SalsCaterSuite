namespace SalsCaterSuite.Application.Features.Bookings.Services;

/// <summary>
/// The menu-change window of one booking (BR-36, DEC-128).
/// </summary>
/// <param name="LockDate">Always a concrete date — never <c>null</c> (DEC-128).</param>
/// <param name="LockDays">
/// The configured days, or <c>null</c> when the configuration key is absent. The "not stated"
/// state belongs to the <b>days</b>, never to the date.
/// </param>
/// <param name="IsLocked">Whether the lock date has passed.</param>
public sealed record BookingMenuWindowState(DateOnly LockDate, int? LockDays, bool IsLocked);

/// <summary>
/// Resolves the <b>menu-change window</b> (BR-36, DEC-128):
/// <c>LockDate = EventDate - LockDays</c>, where <c>LockDays</c> is read from
/// <c>Bookings:MenuSelection:LockDaysBeforeEvent</c>.
/// <para>
/// <b>Absent</b>, the lock date is the <b>event date</b> — the selection is editable until the
/// event begins. A configuration of zero or less is treated the same way (there is nothing to
/// subtract), while the stated value is still published. The write is refused when
/// <c>today &gt;= lockDate</c>.
/// </para>
/// </summary>
public static class BookingMenuWindow
{
    /// <summary>Resolves the window of a booking that takes place on <paramref name="eventDate"/>.</summary>
    public static BookingMenuWindowState Resolve(DateOnly eventDate, int? lockDays, DateOnly today)
    {
        var lockDate = lockDays.HasValue && lockDays.Value > 0
            ? eventDate.AddDays(-lockDays.Value)
            : eventDate;

        return new BookingMenuWindowState(lockDate, lockDays, today >= lockDate);
    }
}
