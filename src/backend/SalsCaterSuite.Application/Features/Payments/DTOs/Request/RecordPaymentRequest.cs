using SalsCaterSuite.Domain.Enums;

namespace SalsCaterSuite.Application.Features.Payments.DTOs.Request;

public class RecordPaymentRequest
{
    public int TransactionId { get; set; }
    public decimal AmountPaid { get; set; }
    public string Particular { get; set; } = "Downpayment";
    public PaymentMeans PaymentMeans { get; set; }
    public string? CheckNo { get; set; }
    public string? Notes { get; set; }
}
