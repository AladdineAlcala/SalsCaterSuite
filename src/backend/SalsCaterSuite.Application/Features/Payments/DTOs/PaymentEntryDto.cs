using SalsCaterSuite.Application.Features.Payments.DTOs.Response;
using SalsCaterSuite.Domain.Enums;

namespace SalsCaterSuite.Application.Features.Payments.DTOs;

public class LedgerSummaryDto
{
    public int TransactionId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string Occasion { get; set; } = string.Empty;
    public DateOnly EventDate { get; set; }
    public decimal ContractAmount { get; set; }
    public decimal TotalPaid { get; set; }
    public decimal TotalRefunded { get; set; }
    public decimal BalanceDue { get; set; }
    public bool IsFullyPaid { get; set; }
    public List<PaymentEntryResponse> Payments { get; set; } = new();
}
