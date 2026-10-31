using SalsCaterSuite.Domain.Enums;

namespace SalsCaterSuite.Application.Features.Payments.DTOs.Response;

public class PaymentEntryResponse
{
    public string PaymentNo { get; set; } = string.Empty;
    public int TransactionId { get; set; }
    public DateTime DateOfPayment { get; set; }
    public string? Particular { get; set; }
    public PaymentMeans PaymentMeans { get; set; }
    public decimal AmountPaid { get; set; }
    public string? CheckNo { get; set; }
    public string? Notes { get; set; }
    public string? CreatedByUser { get; set; }
}
