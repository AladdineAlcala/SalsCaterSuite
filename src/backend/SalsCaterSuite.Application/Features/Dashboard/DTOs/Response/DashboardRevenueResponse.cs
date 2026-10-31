using SalsCaterSuite.Domain.Enums;

namespace SalsCaterSuite.Application.Features.Dashboard.DTOs.Response;

/// <summary>KPI-01 / KPI-08 — net contracted catering revenue for the period.</summary>
public record DashboardRevenueResponse
{
    /// <summary>Σ net amount due over the period population.</summary>
    public required decimal NetContractedRevenue { get; init; }

    /// <summary>Revenue ÷ booking count; <c>null</c> when there are no bookings in the period.</summary>
    public required decimal? AverageBookingValue { get; init; }

    /// <summary>The size of the period population.</summary>
    public required int BookingCount { get; init; }
}
