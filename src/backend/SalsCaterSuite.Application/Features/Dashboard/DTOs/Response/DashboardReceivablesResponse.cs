using SalsCaterSuite.Domain.Enums;

namespace SalsCaterSuite.Application.Features.Dashboard.DTOs.Response;

/// <summary>KPI-12 / KPI-13 — accounts receivable, its aging and its overdue portion.</summary>
public record DashboardReceivablesResponse
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
    public required IReadOnlyList<DashboardAgingBucketResponse> AgingBuckets { get; init; }
}
