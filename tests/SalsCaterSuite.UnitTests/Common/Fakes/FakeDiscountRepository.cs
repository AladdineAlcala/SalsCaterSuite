namespace SalsCaterSuite.UnitTests.Common.Fakes;

using SalsCaterSuite.Application.Common.Interfaces;
using SalsCaterSuite.Domain.Entities;

/// <summary>
/// Hand-written fake of <see cref="IDiscountRepository"/> (DEC-037). No EF Core is involved.
/// <para>
/// The catalogue is read by <b>code</b>, and the code carries no unique index — so the fake
/// returns <i>every</i> live match and lets the Application layer decide that an unknown and an
/// ambiguous code are both a 400 (BR-02).
/// </para>
/// </summary>
public sealed class FakeDiscountRepository : IDiscountRepository
{
    private readonly FakeSalsCaterSuiteDataStore _store;

    public FakeDiscountRepository(FakeSalsCaterSuiteDataStore store)
    {
        _store = store;
    }

    public Task<IReadOnlyList<Discount>> GetActiveByCodeAsync(
        string discountCode,
        CancellationToken cancellationToken = default)
    {
        var code = discountCode?.Trim() ?? string.Empty;

        var result = _store.Discounts
            .Where(discount => !discount.IsDeleted && discount.IsActive && discount.DiscountCode == code)
            .OrderBy(discount => discount.DiscountId)
            .ToList();

        return Task.FromResult<IReadOnlyList<Discount>>(result);
    }
}
