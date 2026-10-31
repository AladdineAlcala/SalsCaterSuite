using SalsCaterSuite.Domain.Enums;

namespace SalsCaterSuite.Application.Features.Dashboard.DTOs.Response;

/// <summary>The resolved reporting period (contract §9.1).</summary>
public record DashboardPeriodResponse
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
