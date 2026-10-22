namespace SalsCaterSuite.UnitTests.Common.Fakes;

using SalsCaterSuite.Application.Common.Interfaces;
using SalsCaterSuite.Application.Features.Packages.DTOs;
using SalsCaterSuite.Domain.Entities;

/// <summary>
/// Hand-written fake of <see cref="IAreaRepository"/> (DEC-037). No EF Core is involved;
/// in-place updates are visible through the shared reference-tracking store.
/// </summary>
public sealed class FakeAreaRepository : IAreaRepository
{
    private readonly FakeSalsCaterSuiteDataStore _store;

    public FakeAreaRepository(FakeSalsCaterSuiteDataStore store)
    {
        _store = store;
    }

    /// <summary>Number of successful saves, for asserting the commit boundary.</summary>
    public int SaveCount { get; private set; }

    public Task<List<AreaDto>> GetCatalogueAsync(CancellationToken cancellationToken = default)
    {
        var result = _store.Areas
            .Where(area => !area.IsDeleted)
            .OrderBy(area => area.AreaDetails)
            .Select(area => new AreaDto
            {
                AreaId = area.AreaId,
                AreaDetails = area.AreaDetails ?? string.Empty,
                AdditionalRate = area.AdditionalRate,
                DistanceKm = area.DistanceKm,
                LogisticsNote = area.LogisticsNote,
                LogisticsCost = area.LogisticsCost
            })
            .ToList();

        return Task.FromResult(result);
    }

    public Task<Area?> GetLiveByIdAsync(int areaId, CancellationToken cancellationToken = default)
    {
        var area = _store.Areas.FirstOrDefault(candidate => candidate.AreaId == areaId && !candidate.IsDeleted);

        return Task.FromResult(area);
    }

    public Task<bool> ExistsAsync(int areaId, CancellationToken cancellationToken = default)
    {
        var exists = _store.Areas.Any(area => area.AreaId == areaId && !area.IsDeleted);

        return Task.FromResult(exists);
    }

    public Task AddAsync(Area area, CancellationToken cancellationToken = default)
    {
        _store.Areas.Add(area);
        return Task.CompletedTask;
    }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        SaveCount++;

        foreach (var area in _store.Areas.Where(area => area.AreaId == 0).ToList())
        {
            _store.AssignAreaId(area);
        }

        return Task.FromResult(1);
    }
}
