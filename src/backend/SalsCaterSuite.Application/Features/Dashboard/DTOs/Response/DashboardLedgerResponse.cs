using SalsCaterSuite.Domain.Enums;

namespace SalsCaterSuite.Application.Features.Dashboard.DTOs.Response;

/// <summary>KPI-17 / KPI-18 / KPI-19 — the payment-ledger tiles.</summary>
public record DashboardLedgerResponse
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
