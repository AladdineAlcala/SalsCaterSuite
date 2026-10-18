namespace SalsCaterSuite.Domain.Enums;

/// <summary>
/// Lifecycle status of a refund entry.
/// Replaces the legacy int? Status = 1 magic-number pattern in Refund.
/// </summary>
public enum RefundStatus
{
    Pending = 0,
    Processed = 1,
    Rejected = 2
}

