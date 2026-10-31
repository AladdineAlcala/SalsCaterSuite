using SalsCaterSuite.Domain.Enums;

namespace SalsCaterSuite.Application.Features.Dashboard.DTOs.Response;

/// <summary>One accounts-receivable aging bucket (KPI-13). The due date is the event date (DEC-041).</summary>
public record DashboardAgingBucketResponse
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
