namespace SalsCaterSuite.UnitTests.Common.Fakes;

using SalsCaterSuite.Application.Common.Interfaces;
using SalsCaterSuite.Application.Common.Models;
using SalsCaterSuite.Domain.Entities;

/// <summary>Hand-written fake of <see cref="IPaymentRepository"/> (DEC-037).</summary>
public sealed class FakePaymentRepository : IPaymentRepository
{
    private readonly FakeSalsCaterSuiteDataStore _store;

    public FakePaymentRepository(FakeSalsCaterSuiteDataStore store)
    {
        _store = store;
    }

    /// <summary>All payments currently in the store — the assertion surface for tests.</summary>
    public IReadOnlyList<Payment> Payments => _store.Payments;

    public Task AddAsync(Payment payment, CancellationToken cancellationToken = default)
    {
        _store.Payments.Add(payment);
        return Task.CompletedTask;
    }

    public void Remove(Payment payment)
    {
        _store.Payments.Remove(payment);
    }

    public Task<int> CountCreatedOnDateAsync(DateTime date, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(
            _store.Payments.Count(p => p.CreatedDate.HasValue && p.CreatedDate.Value.Date == date.Date));
    }

    /// <summary>
    /// Sums and counts the payments inside the inclusive range, excluding soft-deleted rows —
    /// the in-memory mirror of the EF implementation (spec §7 R1).
    /// </summary>
    public Task<PaymentRangeAggregate> GetRangeAggregateAsync(DateTime fromInclusive, DateTime toInclusive, CancellationToken cancellationToken = default)
    {
        var payments = _store.Payments
            .Where(p => !p.IsDeleted && p.DateOfPayment >= fromInclusive && p.DateOfPayment <= toInclusive)
            .ToList();

        return Task.FromResult(new PaymentRangeAggregate(
            payments.Sum(p => p.AmountPaid),
            payments.Count));
    }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(1);
    }
}
