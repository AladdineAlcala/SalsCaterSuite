using SalsCaterSuite.Application.Features.Payments.DTOs;
using SalsCaterSuite.Application.Common.Models;
using SalsCaterSuite.Application.Features.Payments.DTOs.Request;
using SalsCaterSuite.Application.Features.Payments.DTOs.Response;

namespace SalsCaterSuite.Application.Features.Payments.Services;

public interface IPaymentLedgerService
{
    Task<Result<PaymentEntryResponse>> RecordPaymentAsync(RecordPaymentRequest request, CancellationToken cancellationToken = default);
    Task<Result<LedgerSummaryDto>> GetLedgerAsync(int transactionId, CancellationToken cancellationToken = default);
}
