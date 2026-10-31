using SalsCaterSuite.Domain.Enums;

namespace SalsCaterSuite.Application.Features.Dashboard.DTOs.Response;

/// <summary>KPI-02 / KPI-10 / KPI-11 — cash-basis collections for the period.</summary>
public record DashboardCollectionsResponse
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
