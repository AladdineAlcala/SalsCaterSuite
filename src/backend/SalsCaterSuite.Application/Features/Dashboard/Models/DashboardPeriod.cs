using System.Globalization;

namespace SalsCaterSuite.Application.Features.Dashboard.Models;

/// <summary>
/// The resolved reporting period of the operations dashboard (contract §9). The server owns the
/// period boundaries and returns them to the client; the client never recomputes them.
/// <para>
/// A period is always a calendar month. The comparison window for
/// <c>collections.previousPeriodAmount</c> is the <b>immediately preceding calendar month</b>.
/// The contract phrases this as "the immediately preceding period of equal length"; a
/// same-length window that ends where the current period starts would overlap it (for a 31-day
/// month it would run 30 September – 30 October and double-count the current period), so the
/// only non-overlapping reading is the preceding calendar month. The two windows are of the
/// same <i>granularity</i> and differ by at most a few days.
/// </para>
/// </summary>
public record DashboardPeriod(int Year, int Month, DateOnly StartDate, DateOnly EndDate)
{
    /// <summary>The human-readable period label, e.g. <c>October 2026</c>.</summary>
    public string Label => new DateTime(Year, Month, 1).ToString("MMMM yyyy", CultureInfo.InvariantCulture);

    /// <summary>The inclusive lower bound of the period, as a <see cref="DateTime"/>.</summary>
    public DateTime StartDateTime => StartDate.ToDateTime(TimeOnly.MinValue);

    /// <summary>The inclusive upper bound of the period, as a <see cref="DateTime"/>.</summary>
    public DateTime EndDateTime => EndDate.ToDateTime(TimeOnly.MaxValue);

    /// <summary>Builds the period that contains the given calendar month.</summary>
    public static DashboardPeriod ForMonth(int year, int month)
    {
        return new DashboardPeriod(
            year,
            month,
            new DateOnly(year, month, 1),
            new DateOnly(year, month, DateTime.DaysInMonth(year, month)));
    }

    /// <summary>The period that contains the given UTC instant (the default period).</summary>
    public static DashboardPeriod FromUtcNow(DateTime utcNow) => ForMonth(utcNow.Year, utcNow.Month);

    /// <summary>The immediately preceding calendar month.</summary>
    public DashboardPeriod PreviousPeriod
    {
        get
        {
            var previousMonth = StartDate.AddMonths(-1);
            return ForMonth(previousMonth.Year, previousMonth.Month);
        }
    }

    /// <summary>
    /// Parses the <c>period</c> query string, which must be exactly <c>YYYY-MM</c>.
    /// <para>
    /// The round-trip comparison rejects anything that is not literally the requested format
    /// (for example <c>2026-1</c>, <c>2026-13</c>, <c>2026-10-01</c> or a padded value), so a
    /// malformed value is rejected rather than silently coerced.
    /// </para>
    /// </summary>
    /// <param name="value">The raw query-string value.</param>
    /// <param name="period">The parsed period, or <c>default</c> when the value is malformed.</param>
    public static bool TryParse(string? value, out DashboardPeriod period)
    {
        period = default!;

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        if (!DateTime.TryParseExact(
                value,
                "yyyy-MM",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var parsed))
        {
            return false;
        }

        if (!string.Equals(
                parsed.ToString("yyyy-MM", CultureInfo.InvariantCulture),
                value,
                StringComparison.Ordinal))
        {
            return false;
        }

        period = ForMonth(parsed.Year, parsed.Month);
        return true;
    }
}
