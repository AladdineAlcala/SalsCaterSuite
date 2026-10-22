namespace SalsCaterSuite.UnitTests.Common.Fakes;

using SalsCaterSuite.Application.Common.Interfaces;
using SalsCaterSuite.Domain.Entities;

/// <summary>Hand-written fake of <see cref="IRefundRepository"/> (DEC-037).</summary>
public sealed class FakeRefundRepository : IRefundRepository
{
    private readonly FakeSalsCaterSuiteDataStore _store;

    public FakeRefundRepository(FakeSalsCaterSuiteDataStore store)
    {
        _store = store;
    }

    /// <summary>All refunds currently in the store — the assertion surface for tests.</summary>
    public IReadOnlyList<Refund> Refunds => _store.Refunds;

    public Task AddAsync(Refund refund, CancellationToken cancellationToken = default)
    {
        _store.Refunds.Add(refund);
        return Task.CompletedTask;
    }
}
