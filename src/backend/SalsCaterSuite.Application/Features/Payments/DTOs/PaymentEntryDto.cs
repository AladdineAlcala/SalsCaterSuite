namespace SalsCaterSuite.Application.Features.Payments.DTOs;

public class PaymentEntryDto
{
    public string PaymentNo { get; set; } = string.Empty;
    public int TransactionId { get; set; }
    public DateTime DateOfPayment { get; set; }
    public string? Particular { get; set; }
    public string PaymentMeans { get; set; } = "Cash";
    public decimal AmountPaid { get; set; }
    public string? CheckNo { get; set; }
    public string? Notes { get; set; }
    public string? CreatedByUser { get; set; }
}

public class RecordPaymentRequestDto
{
    public int TransactionId { get; set; }
    public decimal AmountPaid { get; set; }
    public string Particular { get; set; } = "Downpayment";
    public string PaymentMeans { get; set; } = "Cash";
    public string? CheckNo { get; set; }
    public string? Notes { get; set; }
}

public class LedgerSummaryDto
{
    public int TransactionId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string Occasion { get; set; } = string.Empty;
    public DateTime? EventDate { get; set; }
    public decimal ContractAmount { get; set; }
    public decimal TotalPaid { get; set; }
    public decimal TotalRefunded { get; set; }
    public decimal BalanceDue { get; set; }
    public bool IsFullyPaid { get; set; }
    public List<PaymentEntryDto> Payments { get; set; } = new();
}

