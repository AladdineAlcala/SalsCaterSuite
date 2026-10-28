namespace SalsCaterSuite.UnitTests.Common.Fakes;

using SalsCaterSuite.Application.Common.Interfaces;
using SalsCaterSuite.Domain.Entities;

/// <summary>
/// Hand-written fake of <see cref="IPackagePaxApplicableRepository"/> (DEC-037). No EF Core is
/// involved; it mirrors the live-only population and the identifier ordering of the real
/// repository.
/// </summary>
public sealed class FakePackagePaxApplicableRepository : IPackagePaxApplicableRepository
{
    private readonly FakeSalsCaterSuiteDataStore _store;

    public FakePackagePaxApplicableRepository(FakeSalsCaterSuiteDataStore store)
    {
        _store = store;
    }

    public Task<IReadOnlyList<PackagePaxApplicable>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        // The live population only, ordered by the fixed identifier — mirroring the repository.
        IReadOnlyList<PackagePaxApplicable> rows = _store.PackagePaxApplicables
            .Where(option => !option.IsDeleted)
            .OrderBy(option => option.NumPaxId)
            .ToList();

        return Task.FromResult(rows);
    }

    public Task<bool> ExistsAsync(int numPaxId, CancellationToken cancellationToken = default)
    {
        // A soft-deleted option does not count: it is no longer a selectable classification.
        return Task.FromResult(_store.PackagePaxApplicables
            .Any(option => option.NumPaxId == numPaxId && !option.IsDeleted));
    }
}
