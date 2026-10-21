using SalsCaterSuite.Application.Common.Interfaces;

namespace SalsCaterSuite.Application.Features.Customers.Services;

/// <summary>
/// The one aging vocabulary used by the Customer Master (spec §3, DEC-052).
/// <para>
/// The keys are exactly the KPI-13 / DEC-041 buckets, plus a <see cref="Clean"/> key for a
/// customer with no positive balance. A third status vocabulary is never created (spec §1.2).
/// </para>
/// <para>
/// The display labels live on the server: the client renders the label it is given and never
/// derives one (AC-C3, AC-C17).
/// </para>
/// </summary>
public static class CustomerAgingBucket
{
    /// <summary>No positive balance — the customer owes nothing.</summary>
    public const string Clean = "Clean";

    /// <summary>The event date is today or later, so nothing is overdue yet.</summary>
    public const string NotYetDue = "NotYetDue";

    /// <summary>1–30 days past the event date (the due date — DEC-041).</summary>
    public const string Days1To30 = "Days1To30";

    /// <summary>31–60 days past the event date.</summary>
    public const string Days31To60 = "Days31To60";

    /// <summary>61–90 days past the event date.</summary>
    public const string Days61To90 = "Days61To90";

    /// <summary>More than 90 days past the event date.</summary>
    public const string Over90Days = "Over90Days";

    public const string CleanLabel = "Paid in full";
    public const string NotYetDueLabel = "Open · not yet due";
    public const string Days1To30Label = "Overdue 1–30 days";
    public const string Days31To60Label = "Overdue 31–60 days";
    public const string Days61To90Label = "Overdue 61–90 days";
    public const string Over90DaysLabel = "Overdue 90+ days";

    // ── The AR-state filter values accepted by the directory endpoint (spec §5.1) ──────────

    /// <summary>Only customers with no positive balance.</summary>
    public const string ArStateClean = "Clean";

    /// <summary>Only customers with an outstanding balance.</summary>
    public const string ArStateOpen = "Open";

    /// <summary>Only customers whose worst bucket is 31 days or more overdue.</summary>
    public const string ArStateOverdue30 = "Overdue30";

    /// <summary>Only customers whose worst bucket is 61 days or more overdue.</summary>
    public const string ArStateOverdue60 = "Overdue60";
}

/// <summary>
/// One booking's contribution to a customer's aging state: the event date (the due date,
/// DEC-041) and the balance the booking contributes to the accounts receivable (DEC-025).
/// </summary>
/// <param name="EventDate">The booking's event date.</param>
/// <param name="Balance">The booking's outstanding balance; only a positive balance ages.</param>
public readonly record struct CustomerAgingBalance(DateOnly EventDate, decimal Balance);

/// <summary>
/// The single aging service shared by the customer directory and the customer statement, so
/// there is exactly one aging vocabulary in the product (spec §3, DEC-052).
/// </summary>
public interface ICustomerAgingService
{
    /// <summary>
    /// The server's "today". It comes from <see cref="IDateTime"/> so the unit tests are
    /// deterministic and so the Customer Master and the operations dashboard age the very same
    /// bookings on the very same day (the dashboard's basis — DEC-041).
    /// </summary>
    DateOnly Today { get; }

    /// <summary>The aging bucket of a single booking (DEC-041).</summary>
    string GetBucketKey(DateOnly eventDate, DateOnly today);

    /// <summary>The server-provided display label of an aging bucket.</summary>
    string GetBucketLabel(string bucketKey);

    /// <summary>
    /// The customer's aging state: the <b>worst</b> (most overdue) bucket among the bookings
    /// that contribute a positive balance, or <see cref="CustomerAgingBucket.Clean"/> when the
    /// customer has none (spec §3).
    /// </summary>
    string GetWorstBucketKey(IEnumerable<CustomerAgingBalance> balances, DateOnly today);

    /// <summary>True when the bucket is more than 30 days overdue (KPI-13's overdue portion).</summary>
    bool IsOverdue30(string bucketKey);

    /// <summary>True when the bucket is more than 60 days overdue.</summary>
    bool IsOverdue60(string bucketKey);
}

/// <summary>
/// The default <see cref="ICustomerAgingService"/>.
/// <para>
/// The bucket boundaries are the KPI-13 table (DEC-041): the due date is the event date, and
/// the days overdue are counted from it. A booking whose event date is today or later is
/// <see cref="CustomerAgingBucket.NotYetDue"/>.
/// </para>
/// </summary>
public class CustomerAgingService : ICustomerAgingService
{
    private readonly IDateTime _dateTime;

    public CustomerAgingService(IDateTime dateTime)
    {
        _dateTime = dateTime;
    }

    public DateOnly Today => DateOnly.FromDateTime(_dateTime.UtcNow);

    public string GetBucketKey(DateOnly eventDate, DateOnly today)
    {
        if (eventDate >= today)
        {
            return CustomerAgingBucket.NotYetDue;
        }

        var daysOverdue = today.DayNumber - eventDate.DayNumber;

        if (daysOverdue <= 30)
        {
            return CustomerAgingBucket.Days1To30;
        }

        if (daysOverdue <= 60)
        {
            return CustomerAgingBucket.Days31To60;
        }

        if (daysOverdue <= 90)
        {
            return CustomerAgingBucket.Days61To90;
        }

        return CustomerAgingBucket.Over90Days;
    }

    public string GetBucketLabel(string bucketKey) => bucketKey switch
    {
        CustomerAgingBucket.Clean => CustomerAgingBucket.CleanLabel,
        CustomerAgingBucket.NotYetDue => CustomerAgingBucket.NotYetDueLabel,
        CustomerAgingBucket.Days1To30 => CustomerAgingBucket.Days1To30Label,
        CustomerAgingBucket.Days31To60 => CustomerAgingBucket.Days31To60Label,
        CustomerAgingBucket.Days61To90 => CustomerAgingBucket.Days61To90Label,
        CustomerAgingBucket.Over90Days => CustomerAgingBucket.Over90DaysLabel,
        _ => throw new ArgumentOutOfRangeException(
            nameof(bucketKey), bucketKey, "Unknown customer aging bucket key.")
    };

    public string GetWorstBucketKey(IEnumerable<CustomerAgingBalance> balances, DateOnly today)
    {
        var worstKey = CustomerAgingBucket.Clean;
        var worstSeverity = Severity(worstKey);

        foreach (var entry in balances)
        {
            // Only a booking that contributes a positive balance ages the customer
            // (spec §3 — "the bookings that contribute a positive balance").
            if (entry.Balance <= 0m)
            {
                continue;
            }

            var key = GetBucketKey(entry.EventDate, today);
            var severity = Severity(key);

            if (severity > worstSeverity)
            {
                worstKey = key;
                worstSeverity = severity;
            }
        }

        return worstKey;
    }

    public bool IsOverdue30(string bucketKey) =>
        bucketKey is CustomerAgingBucket.Days31To60
            or CustomerAgingBucket.Days61To90
            or CustomerAgingBucket.Over90Days;

    public bool IsOverdue60(string bucketKey) =>
        bucketKey is CustomerAgingBucket.Days61To90 or CustomerAgingBucket.Over90Days;

    /// <summary>
    /// The ordering of the buckets, from "nothing owed" to "most overdue".
    /// <see cref="CustomerAgingBucket.Clean"/> ranks below every real bucket so that a single
    /// positive-balance booking always produces a state.
    /// </summary>
    private static int Severity(string bucketKey) => bucketKey switch
    {
        CustomerAgingBucket.Clean => -1,
        CustomerAgingBucket.NotYetDue => 0,
        CustomerAgingBucket.Days1To30 => 1,
        CustomerAgingBucket.Days31To60 => 2,
        CustomerAgingBucket.Days61To90 => 3,
        CustomerAgingBucket.Over90Days => 4,
        _ => throw new ArgumentOutOfRangeException(
            nameof(bucketKey), bucketKey, "Unknown customer aging bucket key.")
    };
}
