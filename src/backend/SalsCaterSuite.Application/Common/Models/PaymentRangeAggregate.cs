namespace SalsCaterSuite.Application.Common.Models;

/// <summary>
/// The aggregate of the payment rows whose <c>DateOfPayment</c> falls inside an inclusive
/// date range (KPI-02, DEC-012). The sum is taken from the <c>Payment</c> rows, which are the
/// authoritative amount-paid source — never the denormalized <c>Booking.AmountPaid</c> cache.
/// </summary>
public record PaymentRangeAggregate(decimal TotalAmount, int PaymentCount);
