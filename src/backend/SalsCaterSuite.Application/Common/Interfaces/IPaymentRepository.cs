namespace SalsCaterSuite.Application.Common.Interfaces;

using SalsCaterSuite.Application.Common.Models;
using SalsCaterSuite.Domain.Entities;

/// <summary>
/// Payment-entry persistence (DEC-037, DEC-012). Expressed in terms of the
/// <see cref="Payment"/> domain entity only.
/// </summary>
public interface IPaymentRepository
{
    Task AddAsync(Payment payment, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a payment that was staged but must not be persisted (the domain
    /// rejected the operation). Safe to call on an entity that was only staged.
    /// </summary>
    void Remove(Payment payment);

    /// <summary>
    /// Counts payments created on the given calendar day — the receipt-numbering
    /// sequence used by <c>PaymentLedgerService</c>.
    /// </summary>
    Task<int> CountCreatedOnDateAsync(DateTime date, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sums and counts the payments whose payment date falls inside the inclusive range,
    /// excluding soft-deleted rows (REQ-DSH-01, spec §7 R1, DEC-012).
    /// <para>
    /// The sum is taken from the <c>Payment</c> rows, which are the authoritative amount-paid
    /// source — never the denormalized <c>Booking.AmountPaid</c> cache.
    /// </para>
    /// </summary>
    Task<PaymentRangeAggregate> GetRangeAggregateAsync(DateTime fromInclusive, DateTime toInclusive, CancellationToken cancellationToken = default);

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
