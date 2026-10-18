using SalsCaterSuite.Application.Common.Models;
using SalsCaterSuite.Application.Features.Payments.DTOs;

namespace SalsCaterSuite.Application.Features.Payments.Services;

public interface IPaymentLedgerService
{
    Task<Result<PaymentEntryDto>> RecordPaymentAsync(RecordPaymentRequestDto request, CancellationToken cancellationToken = default);
    Task<Result<LedgerSummaryDto>> GetLedgerAsync(int transactionId, CancellationToken cancellationToken = default);
}

