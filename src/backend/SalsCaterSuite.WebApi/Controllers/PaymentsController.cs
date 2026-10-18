using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SalsCaterSuite.Application.Features.Payments.DTOs;
using SalsCaterSuite.Application.Features.Payments.Services;

namespace SalsCaterSuite.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PaymentsController : ControllerBase
{
    private readonly IPaymentLedgerService _ledgerService;

    public PaymentsController(IPaymentLedgerService ledgerService)
    {
        _ledgerService = ledgerService;
    }

    /// <summary>POST /api/payments — Record a payment for a booking</summary>
    [HttpPost]
    public async Task<IActionResult> RecordPayment([FromBody] RecordPaymentRequestDto request, CancellationToken cancellationToken)
    {
        var result = await _ledgerService.RecordPaymentAsync(request, cancellationToken);
        return result.Succeeded
            ? Ok(result.Data)
            : BadRequest(new { errors = result.Errors });
    }

    /// <summary>GET /api/payments/ledger/{transactionId} — Get ledger for a booking</summary>
    [HttpGet("ledger/{transactionId:int}")]
    public async Task<IActionResult> GetLedger(int transactionId, CancellationToken cancellationToken)
    {
        var result = await _ledgerService.GetLedgerAsync(transactionId, cancellationToken);
        return result.Succeeded
            ? Ok(result.Data)
            : NotFound(new { errors = result.Errors });
    }
}

