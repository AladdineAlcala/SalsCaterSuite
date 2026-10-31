using SalsCaterSuite.Domain.Enums;

namespace SalsCaterSuite.Application.Features.Dashboard.DTOs.Response;

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
public record DashboardResponse
{
    /// <summary>The resolved reporting period and its server-computed boundaries.</summary>
    public required DashboardPeriodResponse Period { get; init; }

    /// <summary>The deployment-configured margin benchmark (DEC-040).</summary>
    public required DashboardBenchmarksResponse Benchmarks { get; init; }

    /// <summary>KPI-07 — active bookings for the period, split into committed / tentative / other.</summary>
    public required DashboardBookingsResponse Bookings { get; init; }

    /// <summary>KPI-01 / KPI-08 / KPI-04 / KPI-09 — net contracted revenue and average booking value.</summary>
    public required DashboardRevenueResponse Revenue { get; init; }

    /// <summary>KPI-02 / KPI-10 / KPI-11 — cash-basis collections and the indicative collection ratio.</summary>
    public required DashboardCollectionsResponse Collections { get; init; }

    /// <summary>KPI-12 / KPI-13 — total accounts receivable, its aging buckets and the overdue portion.</summary>
    public required DashboardReceivablesResponse Receivables { get; init; }

    /// <summary>KPI-14 — dish-costing coverage of the menu catalogue (a data-completeness metric).</summary>
    public required DashboardDishCostingResponse DishCosting { get; init; }

    /// <summary>KPI-15 — catalogue gross margin and its variance to the configured benchmark.</summary>
    public required DashboardMarginResponse Margin { get; init; }

    /// <summary>KPI-16 — food-cost share per course category, ordered by amount descending.</summary>
    public required IReadOnlyList<DashboardFoodCostByCategoryResponse> FoodCostByCategory { get; init; }

    /// <summary>The next events on the banquet schedule (at most five).</summary>
    public required IReadOnlyList<DashboardUpcomingBookingResponse> UpcomingBookings { get; init; }

    /// <summary>KPI-17 / KPI-18 / KPI-19 — the payment-ledger tiles.</summary>
    public required DashboardLedgerResponse Ledger { get; init; }

    /// <summary>The server UTC timestamp at which the aggregate was produced.</summary>
    public required DateTime GeneratedAt { get; init; }
}
