using SalsCaterSuite.Domain.Enums;

namespace SalsCaterSuite.Application.Features.Dashboard.DTOs.Response;

/// <summary>KPI-07 — the period's booking population, split by commitment.</summary>
public record DashboardBookingsResponse
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
