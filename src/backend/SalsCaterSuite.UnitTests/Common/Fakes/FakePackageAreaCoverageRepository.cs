namespace SalsCaterSuite.UnitTests.Common.Fakes;

using SalsCaterSuite.Application.Common.Interfaces;
using SalsCaterSuite.Application.Features.Packages.DTOs;
using SalsCaterSuite.Domain.Entities;

/// <summary>
/// Hand-written fake of <see cref="IPackageAreaCoverageRepository"/> (DEC-037). No EF Core is
/// involved; in-place updates are visible through the shared reference-tracking store.
/// <para>
/// The projection mirrors the EF implementation: a coverage row whose <b>municipality</b> is
/// soft-deleted is excluded, because the municipality catalogue is the population (contract
/// §13.4).
/// </para>
/// </summary>
public sealed class FakePackageAreaCoverageRepository : IPackageAreaCoverageRepository
{
    private readonly FakeSalsCaterSuiteDataStore _store;

    public FakePackageAreaCoverageRepository(FakeSalsCaterSuiteDataStore store)
    {
        _store = store;
    }

    /// <summary>Number of successful saves, for asserting the commit boundary.</summary>
    public int SaveCount { get; private set; }

    public Task<IReadOnlyList<PackageAreaCoverageProjection>> GetLiveCoveragesAsync(
        int packageId,
        CancellationToken cancellationToken = default)
    {
        var live = _store.PackageAreaCoverages
            .Where(coverage => coverage.PackageId == packageId && !coverage.IsDeleted)
            .ToList();

        var result = new List<PackageAreaCoverageProjection>();

        foreach (var coverage in live)
        {
            var area = _store.Areas.FirstOrDefault(candidate =>
                candidate.AreaId == coverage.AreaId && !candidate.IsDeleted);

            // A coverage row whose municipality is soft-deleted is excluded (contract §13.4).
            if (area is null)
            {
                continue;
            }

            result.Add(Map(coverage, area));
        }

        return Task.FromResult<IReadOnlyList<PackageAreaCoverageProjection>>(result);
    }

    public Task<IReadOnlyList<PackageAreaCoverage>> GetAllCoveragesAsync(
        int packageId,
        CancellationToken cancellationToken = default)
    {
        var result = _store.PackageAreaCoverages
            .Where(coverage => coverage.PackageId == packageId)
            .ToList();

        return Task.FromResult<IReadOnlyList<PackageAreaCoverage>>(result);
    }

    public Task AddAsync(PackageAreaCoverage coverage, CancellationToken cancellationToken = default)
    {
        _store.PackageAreaCoverages.Add(coverage);
        return Task.CompletedTask;
    }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        SaveCount++;

        foreach (var coverage in _store.PackageAreaCoverages.Where(coverage => coverage.PackageAreaCoverageId == 0).ToList())
        {
            _store.AssignPackageAreaCoverageId(coverage);
        }

        return Task.FromResult(1);
    }

    private PackageAreaCoverageProjection Map(PackageAreaCoverage coverage, Area area) => new()    {
        PackageAreaCoverageId = coverage.PackageAreaCoverageId,
        PackageId = coverage.PackageId,
        AreaId = coverage.AreaId,
        AreaName = area.AreaDetails,
        AdditionalRate = area.AdditionalRate,
        DistanceKm = area.DistanceKm,
        LogisticsNote = area.LogisticsNote,
        LogisticsCost = area.LogisticsCost,
        IsActive = coverage.IsActive
    };
}
