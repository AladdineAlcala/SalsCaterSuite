using SalsCaterSuite.Domain.Enums;

namespace SalsCaterSuite.Application.Features.Bookings.DTOs.Request;

/// <summary>
/// The initial payment of a confirmed booking (contract §14.4).
/// </summary>
public class BookingInitialPaymentRequest
{
    /// <summary>The amount collected. It must not exceed the net amount due (BR-05).</summary>
    public decimal AmountPaid { get; set; }

    /// <summary>The means of payment — the <c>PaymentMeans</c> <b>enum name</b> (contract §8).</summary>
    public PaymentMeans PaymentMeans { get; set; } = PaymentMeans.Cash;

    /// <summary>The cheque number, when the means is a cheque.</summary>
    public string? CheckNo { get; set; }

    /// <summary>Free-text notes.</summary>
    public string? Notes { get; set; }

    /// <summary>The description of the payment — e.g. "Downpayment".</summary>
    public string? Particular { get; set; }
}
