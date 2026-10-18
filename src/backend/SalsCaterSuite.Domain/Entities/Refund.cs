using SalsCaterSuite.Domain.Enums;

namespace SalsCaterSuite.Domain.Entities;

/// <summary>
/// A refund issued against a cancelled or partially-reversed booking.
/// </summary>
public class Refund
{
    public long RefundId { get; set; }

    /// <summary>FK to Booking.TransactionId. Non-nullable — a refund must belong to a booking.</summary>
    public int TransactionId { get; set; }

    public DateTime RefundDate { get; set; } = DateTime.UtcNow;
    public string? RefundReason { get; set; }

    /// <summary>Gross refund amount before any cancellation deductions.</summary>
    public decimal RefundAmount { get; set; }

    /// <summary>Cancellation fee or deduction withheld from the gross refund.</summary>
    public decimal Deduction { get; set; }

    /// <summary>Net amount actually returned to the customer = RefundAmount − Deduction.</summary>
    public decimal NetAmount { get; set; }

    /// <summary>Workflow status of this refund. Replaces the legacy int? Status magic number.</summary>
    public RefundStatus Status { get; set; } = RefundStatus.Pending;

    public Booking Booking { get; set; } = null!;
}
