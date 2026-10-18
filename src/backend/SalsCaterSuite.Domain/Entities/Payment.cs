using SalsCaterSuite.Domain.Common;
using SalsCaterSuite.Domain.Enums;

namespace SalsCaterSuite.Domain.Entities;

/// <summary>
/// A single payment entry against a booking. PaymentNo is the business key.
/// </summary>
public class Payment : BaseEntity
{
    public string PaymentNo { get; set; } = string.Empty;

    /// <summary>FK to Booking.TransactionId. Non-nullable — a payment must belong to a booking.</summary>
    public int TransactionId { get; set; }

    public DateTime DateOfPayment { get; set; } = DateTime.UtcNow;
    public string? Particular { get; set; }
    public PaymentMeans PaymentMeans { get; set; } = PaymentMeans.Cash;
    public decimal AmountPaid { get; set; }
    public string? CheckNo { get; set; }
    public string? Notes { get; set; }

    public Booking Booking { get; set; } = null!;
}
