using SalsCaterSuite.Domain.Enums;

namespace SalsCaterSuite.Application.Features.Dashboard.DTOs;

/// <summary>
/// The read-only operations-dashboard aggregate (contract §9, REQ-DSH-01).
/// <para>
/// Every figure in this payload is computed server-side from authoritative records
/// (DEC-002, DEC-012, DEC-025). The client formats and displays; it never derives a
/// financial figure from this payload.
/// </para>
/// <para>
/// A <c>null</c> ratio means "undefined" — never <c>0</c> and never <c>100</c> (contract §9.2).
/// </para>
/// </summary>
public record DashboardDto
{
    /// <summary>The resolved reporting period and its server-computed boundaries.</summary>
    public required DashboardPeriodDto Period { get; init; }

    /// <summary>The deployment-configured margin benchmark (DEC-040).</summary>
    public required DashboardBenchmarksDto Benchmarks { get; init; }

    /// <summary>KPI-07 — active bookings for the period, split into committed / tentative / other.</summary>
    public required DashboardBookingsDto Bookings { get; init; }

    /// <summary>KPI-01 / KPI-08 / KPI-04 / KPI-09 — net contracted revenue and average booking value.</summary>
    public required DashboardRevenueDto Revenue { get; init; }

    /// <summary>KPI-02 / KPI-10 / KPI-11 — cash-basis collections and the indicative collection ratio.</summary>
    public required DashboardCollectionsDto Collections { get; init; }

    /// <summary>KPI-12 / KPI-13 — total accounts receivable, its aging buckets and the overdue portion.</summary>
    public required DashboardReceivablesDto Receivables { get; init; }

    /// <summary>KPI-14 — dish-costing coverage of the menu catalogue (a data-completeness metric).</summary>
    public required DashboardDishCostingDto DishCosting { get; init; }

    /// <summary>KPI-15 — catalogue gross margin and its variance to the configured benchmark.</summary>
    public required DashboardMarginDto Margin { get; init; }

    /// <summary>KPI-16 — food-cost share per course category, ordered by amount descending.</summary>
    public required IReadOnlyList<DashboardFoodCostByCategoryDto> FoodCostByCategory { get; init; }

    /// <summary>The next events on the banquet schedule (at most five).</summary>
    public required IReadOnlyList<DashboardUpcomingBookingDto> UpcomingBookings { get; init; }

    /// <summary>KPI-17 / KPI-18 / KPI-19 — the payment-ledger tiles.</summary>
    public required DashboardLedgerDto Ledger { get; init; }

    /// <summary>The server UTC timestamp at which the aggregate was produced.</summary>
    public required DateTime GeneratedAt { get; init; }
}

/// <summary>The resolved reporting period (contract §9.1).</summary>
public record DashboardPeriodDto
{
    public required int Year { get; init; }

    public required int Month { get; init; }

    /// <summary>e.g. <c>October 2026</c>.</summary>
    public required string Label { get; init; }

    /// <summary>The first day of the period, inclusive.</summary>
    public required DateOnly StartDate { get; init; }

    /// <summary>The last day of the period, inclusive.</summary>
    public required DateOnly EndDate { get; init; }
}

/// <summary>The margin benchmark that travels with the payload (contract §9.2 rule 7).</summary>
public record DashboardBenchmarksDto
{
    /// <summary>The configured target food-cost percentage (default 35 %).</summary>
    public required decimal TargetFoodCostPercentage { get; init; }

    /// <summary><c>100 − TargetFoodCostPercentage</c>.</summary>
    public required decimal TargetMarginPercentage { get; init; }

    /// <summary><c>false</c> when the configuration key was absent and the default was substituted.</summary>
    public required bool IsConfigured { get; init; }
}

/// <summary>KPI-07 — the period's booking population, split by commitment.</summary>
public record DashboardBookingsDto
{
    /// <summary>Bookings with an event date in the period and a status other than cancelled or expired.</summary>
    public required int TotalCount { get; init; }

    /// <summary><c>Confirmed</c>, <c>DepositPaid</c> or <c>FullyPaid</c>.</summary>
    public required int CommittedCount { get; init; }

    /// <summary><c>Pending</c> — created, not yet confirmed.</summary>
    public required int TentativeCount { get; init; }

    /// <summary><c>InProgress</c>, <c>Completed</c>, <c>NoShow</c> or <c>Rescheduled</c>.</summary>
    public required int OtherCount { get; init; }

    /// <summary>Σ net amount due over the population.</summary>
    public required decimal ContractedValue { get; init; }

    /// <summary>Σ heads over the population.</summary>
    public required int TotalPax { get; init; }
}

/// <summary>KPI-01 / KPI-08 — net contracted catering revenue for the period.</summary>
public record DashboardRevenueDto
{
    /// <summary>Σ net amount due over the period population.</summary>
    public required decimal NetContractedRevenue { get; init; }

    /// <summary>Revenue ÷ booking count; <c>null</c> when there are no bookings in the period.</summary>
    public required decimal? AverageBookingValue { get; init; }

    /// <summary>The size of the period population.</summary>
    public required int BookingCount { get; init; }
}

/// <summary>KPI-02 / KPI-10 / KPI-11 — cash-basis collections for the period.</summary>
public record DashboardCollectionsDto
{
    /// <summary>Σ payment amount where the payment date falls in the period.</summary>
    public required decimal Amount { get; init; }

    /// <summary>The number of payment rows in the period.</summary>
    public required int ReceiptCount { get; init; }

    /// <summary>Σ payment amount over the immediately preceding period.</summary>
    public required decimal PreviousPeriodAmount { get; init; }

    /// <summary>Period-over-period change; <c>null</c> when the previous period collected nothing.</summary>
    public required decimal? ChangePercentage { get; init; }

    /// <summary>
    /// Collections ÷ net contracted revenue. <c>null</c> when the period booked no revenue.
    /// Indicative only — the numerator is cash-basis, the denominator accrual-basis.
    /// </summary>
    public required decimal? CollectionRatioPercentage { get; init; }
}

/// <summary>KPI-12 / KPI-13 — accounts receivable, its aging and its overdue portion.</summary>
public record DashboardReceivablesDto
{
    /// <summary>Σ balance due over the non-cancelled bookings.</summary>
    public required decimal OutstandingAmount { get; init; }

    /// <summary>The number of bookings that contribute a positive balance.</summary>
    public required int BookingCount { get; init; }

    /// <summary>Bookings more than 30 days past their event date.</summary>
    public required int OverdueCount { get; init; }

    /// <summary>The amount more than 30 days past the event date.</summary>
    public required decimal OverdueAmount { get; init; }

    /// <summary>The five aging buckets; their amounts sum exactly to <see cref="OutstandingAmount"/>.</summary>
    public required IReadOnlyList<DashboardAgingBucketDto> AgingBuckets { get; init; }
}

/// <summary>One accounts-receivable aging bucket (KPI-13). The due date is the event date (DEC-041).</summary>
public record DashboardAgingBucketDto
{
    /// <summary>The stable machine key, e.g. <c>Days31To60</c>.</summary>
    public required string Key { get; init; }

    /// <summary>The display label, e.g. <c>31-60 days</c>.</summary>
    public required string Label { get; init; }

    /// <summary>The number of bookings in the bucket.</summary>
    public required int BookingCount { get; init; }

    /// <summary>The total balance in the bucket.</summary>
    public required decimal Amount { get; init; }
}

/// <summary>KPI-14 — dish-costing coverage of the menu catalogue.</summary>
public record DashboardDishCostingDto
{
    /// <summary>Number of non-deleted menus.</summary>
    public required int TotalMenuCount { get; init; }

    /// <summary>Of those, the menus whose food cost is known and greater than zero.</summary>
    public required int CostedMenuCount { get; init; }

    /// <summary><c>TotalMenuCount − CostedMenuCount</c>.</summary>
    public required int UncostedMenuCount { get; init; }

    /// <summary>Costed ÷ total × 100; <c>0</c> when the catalogue is empty.</summary>
    public required decimal CoveragePercentage { get; init; }
}

/// <summary>KPI-15 — catalogue gross margin (a catalogue measure, not a banquet measure).</summary>
public record DashboardMarginDto
{
    /// <summary>Σ price over the costed, priced menus.</summary>
    public required decimal CatalogueRevenueAmount { get; init; }

    /// <summary>Σ food cost over the same population.</summary>
    public required decimal CatalogueFoodCostAmount { get; init; }

    /// <summary>Catalogue revenue − catalogue food cost.</summary>
    public required decimal CatalogueMarginAmount { get; init; }

    /// <summary>Margin ÷ catalogue revenue × 100; <c>null</c> when the catalogue revenue is zero.</summary>
    public required decimal? CatalogueMarginPercentage { get; init; }

    /// <summary>Margin percentage − the configured target margin; <c>null</c> when the margin is <c>null</c>.</summary>
    public required decimal? VarianceToBenchmarkPercentage { get; init; }
}

/// <summary>KPI-16 — the food-cost share of one course category.</summary>
public record DashboardFoodCostByCategoryDto
{
    /// <summary>The course id; <c>null</c> for the uncategorised group.</summary>
    public required int? CourseId { get; init; }

    /// <summary>The course name, or <c>Uncategorised</c> for menus without a course.</summary>
    public required string CourseName { get; init; }

    /// <summary>Σ food cost of the costed menus in this category.</summary>
    public required decimal FoodCostAmount { get; init; }

    /// <summary>This category's share of the catalogue's total food cost.</summary>
    public required decimal SharePercentage { get; init; }

    /// <summary>The number of costed menus in this category.</summary>
    public required int MenuCount { get; init; }
}

/// <summary>One entry of the upcoming banquet schedule.</summary>
public record DashboardUpcomingBookingDto
{
    public required int TransactionId { get; init; }

    /// <summary>The contractual booking number (BR-09, DEC-016).</summary>
    public required string BookingNumber { get; init; }

    public required string CustomerName { get; init; }

    public required string Occasion { get; init; }

    public required DateOnly EventDate { get; init; }

    public required int NumberOfHeads { get; init; }

    /// <summary>The persisted BR-10 lifecycle status, serialised as its string name.</summary>
    public required BookingStatus Status { get; init; }

    /// <summary>The frozen package name; <c>null</c> for a bespoke booking.</summary>
    public required string? PackageName { get; init; }

    /// <summary>The same net-due path the ledger uses (DEC-042).</summary>
    public required decimal NetAmountDue { get; init; }

    /// <summary>Σ payment amount (authoritative, DEC-012).</summary>
    public required decimal AmountPaid { get; init; }

    /// <summary><c>max(0, net amount due − amount paid)</c>.</summary>
    public required decimal Balance { get; init; }
}

/// <summary>KPI-17 / KPI-18 / KPI-19 — the payment-ledger tiles.</summary>
public record DashboardLedgerDto
{
    /// <summary>Σ payment amount over the trailing seven calendar days.</summary>
    public required decimal CollectedLast7DaysAmount { get; init; }

    /// <summary>The number of payment rows over the trailing seven calendar days.</summary>
    public required int CollectedLast7DaysReceiptCount { get; init; }

    /// <summary>Non-cancelled bookings with no payment recorded.</summary>
    public required int AwaitingDepositBookingCount { get; init; }

    /// <summary>Σ net amount due over the bookings that are awaiting a deposit.</summary>
    public required decimal AwaitingDepositAmount { get; init; }

    /// <summary>The number of payment rows whose payment date falls in the active period.</summary>
    public required int ReceiptsIssuedCount { get; init; }
}
