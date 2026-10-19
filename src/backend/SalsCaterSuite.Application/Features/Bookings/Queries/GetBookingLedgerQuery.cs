using SalsCaterSuite.Application.Common.Models;
using SalsCaterSuite.Application.Features.Payments.DTOs;
using SalsCaterSuite.Application.Features.Payments.Services;

namespace SalsCaterSuite.Application.Features.Bookings.Queries;

/// <summary>
/// Booking-scoped ledger entry point. This is intentionally a thin delegate to
/// <see cref="IPaymentLedgerService"/> — the single authoritative ledger
/// implementation (DEC-015). It exists so the bookings route keeps its own
/// application entry point while sharing the exact same projection as the
/// payments route.
/// </summary>
public class GetBookingLedgerQuery
{
    private readonly IPaymentLedgerService _ledgerService;

    public GetBookingLedgerQuery(IPaymentLedgerService ledgerService)
    {
        _ledgerService = ledgerService;
    }

    public Task<Result<LedgerSummaryDto>> ExecuteAsync(int transactionId, CancellationToken cancellationToken = default)
        => _ledgerService.GetLedgerAsync(transactionId, cancellationToken);
}
