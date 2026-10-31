using SalsCaterSuite.Application.Features.Bookings.DTOs.Request;
using SalsCaterSuite.Application.Features.Bookings.DTOs.Response;
using SalsCaterSuite.Application.Features.Bookings.Models;
using SalsCaterSuite.Application.Services;
using SalsCaterSuite.Domain.Entities;

namespace SalsCaterSuite.Application.Features.Bookings.Services;

/// <summary>
/// One booking of the register, with its derived figures.
/// <para>
/// The net amount due comes from the one shared calculator, the collected amount from the
/// authoritative payment sum, and the payment state and the overdue flag from the one
/// classifier (BR-25, BR-27, BR-28).
/// </para>
/// </summary>
public sealed record BookingRegisterRow(
    Booking Booking,
    decimal NetAmountDue,
    decimal Collected,
    decimal Outstanding,
    string PaymentState,
    bool IsOverdue,
    int? DaysUntilEvent,
    DateOnly MenuSelectionLockDate,
    bool IsMenuSelectionLocked);

/// <summary>
/// The one projection and the one filter vocabulary of the Bookings &amp; Events Register
/// (spec §3.3, BR-28, BR-32).
/// <para>
/// The list, the totals and the summary all describe the <b>same</b> population, so the row
/// derivation and the filter rules live here and are called from both — a page and a summary
/// can therefore never describe two different books of work.
/// </para>
/// </summary>
public static class BookingRegisterProjection
{
    /// <summary>
    /// Derives one register row. The caller supplies the calculator's breakdown so the net
    /// amount due comes from the one shared path (BR-25), and the configured menu-change lead
    /// time so the window is read exactly as the selection surface reads it (DEC-128).
    /// </summary>
    public static BookingRegisterRow Project(
        Booking booking,
        BookingCalculationBreakdown breakdown,
        DateOnly today,
        int? menuSelectionLockDays)
    {
        var collected = breakdown.TotalPayments;
        var outstanding = breakdown.RemainingBalance;

        var window = BookingMenuWindow.Resolve(booking.EventDate, menuSelectionLockDays, today);

        return new BookingRegisterRow(
            Booking: booking,
            NetAmountDue: breakdown.NetAmountDue,
            Collected: collected,
            Outstanding: outstanding,
            PaymentState: BookingPaymentStateClassifier.Classify(collected, breakdown.NetAmountDue),
            IsOverdue: BookingPaymentStateClassifier.IsOverdue(outstanding, booking.EventDate, today),
            DaysUntilEvent: BookingPaymentStateClassifier.DaysUntilEvent(booking.EventDate, today),
            MenuSelectionLockDate: window.LockDate,
            IsMenuSelectionLocked: window.IsLocked);
    }

    // ─────────────────────────── the five facet filters (BR-32) ───────────────────────────

    /// <summary>The lifecycle-status filter.</summary>
    public static bool MatchesStatus(BookingRegisterRow row, BookingRegisterFilter filter) =>
        filter.Statuses.Count == 0 || filter.Statuses.Contains(row.Booking.Status);

    /// <summary>The tier-type filter. A booking with no tier never matches a tier filter.</summary>
    public static bool MatchesPackageType(BookingRegisterRow row, BookingRegisterFilter filter) =>
        filter.PackageTypes.Count == 0
        || (row.Booking.Package?.PackageType is string packageType
            && filter.PackageTypes.Contains(packageType));

    /// <summary>The venue filter. A booking with no venue never matches a venue filter.</summary>
    public static bool MatchesVenue(BookingRegisterRow row, BookingRegisterFilter filter) =>
        filter.Venues.Count == 0
        || (row.Booking.Venue is string venue && filter.Venues.Contains(venue));

    /// <summary>The payment-state filter (BR-28).</summary>
    public static bool MatchesPaymentState(BookingRegisterRow row, BookingRegisterFilter filter) =>
        filter.PaymentStates.Count == 0 || filter.PaymentStates.Contains(row.PaymentState);

    /// <summary>
    /// The overdue filter — a <b>tri-state</b> (BR-32):
    /// <list type="bullet">
    /// <item><description><b>absent</b> (<c>null</c>) — no overdue filter at all; every
    /// booking is in the population.</description></item>
    /// <item><description><b><c>true</c></b> — only the bookings whose balance is outstanding
    /// and whose event date has passed (DEC-041).</description></item>
    /// <item><description><b><c>false</c></b> — only the bookings that are <b>not</b> overdue.
    /// An explicit <c>false</c> <b>narrows</b> the population; it is never silently treated as
    /// absent, because an operator who believes they have filtered must not be shown the whole
    /// book.</description></item>
    /// </list>
    /// </summary>
    public static bool MatchesOverdue(BookingRegisterRow row, BookingRegisterFilter filter) =>
        filter.Overdue is null || filter.Overdue.Value == row.IsOverdue;

    /// <summary>Applies every filter — the fully filtered population.</summary>
    public static List<BookingRegisterRow> ApplyFilters(
        IEnumerable<BookingRegisterRow> rows,
        BookingRegisterFilter filter) =>
        rows.Where(row =>
                MatchesStatus(row, filter)
                && MatchesPackageType(row, filter)
                && MatchesVenue(row, filter)
                && MatchesPaymentState(row, filter)
                && MatchesOverdue(row, filter))
            .ToList();

    /// <summary>
    /// The four facet populations. Each one has <b>its own dimension removed</b>, so every facet
    /// answers "how many would I get if I chose this?" (BR-32).
    /// </summary>
    public static BookingRegisterFacetsResponse BuildFacets(
        IReadOnlyList<BookingRegisterRow> rows,
        BookingRegisterFilter filter)
    {
        var packageTypes = rows.Where(row =>
                MatchesStatus(row, filter)
                && MatchesVenue(row, filter)
                && MatchesPaymentState(row, filter)
                && MatchesOverdue(row, filter))
            .ToList();

        var venues = rows.Where(row =>
                MatchesStatus(row, filter)
                && MatchesPackageType(row, filter)
                && MatchesPaymentState(row, filter)
                && MatchesOverdue(row, filter))
            .ToList();

        var statuses = rows.Where(row =>
                MatchesPackageType(row, filter)
                && MatchesVenue(row, filter)
                && MatchesPaymentState(row, filter)
                && MatchesOverdue(row, filter))
            .ToList();

        var paymentStates = rows.Where(row =>
                MatchesStatus(row, filter)
                && MatchesPackageType(row, filter)
                && MatchesVenue(row, filter)
                && MatchesOverdue(row, filter))
            .ToList();

        return new BookingRegisterFacetsResponse
        {
            PackageTypes = Facet(packageTypes, row => row.Booking.Package?.PackageType),
            Venues = Facet(venues, row => row.Booking.Venue),
            Statuses = Facet(statuses, row => row.Booking.Status.ToString()),
            PaymentStates = Facet(paymentStates, row => row.PaymentState)
        };
    }

    /// <summary>
    /// Counts a facet's keys. A booking with no tier has no tier type and is therefore absent
    /// from the tier facet (BR-27); a booking with no venue is absent from the venue facet.
    /// </summary>
    private static List<BookingRegisterFacetCountResponse> Facet(
        IEnumerable<BookingRegisterRow> rows,
        Func<BookingRegisterRow, string?> keySelector)
    {
        return rows
            .Select(keySelector)
            .Where(key => !string.IsNullOrWhiteSpace(key))
            .GroupBy(key => key!, StringComparer.Ordinal)
            .Select(group => new BookingRegisterFacetCountResponse { Key = group.Key, Count = group.Count() })
            .OrderByDescending(facet => facet.Count)
            .ThenBy(facet => facet.Key, StringComparer.Ordinal)
            .ToList();
    }
}
