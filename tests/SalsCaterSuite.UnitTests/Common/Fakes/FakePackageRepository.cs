namespace SalsCaterSuite.UnitTests.Common.Fakes;

using SalsCaterSuite.Application.Common.Interfaces;
using SalsCaterSuite.Application.Features.Packages.DTOs;
using SalsCaterSuite.Domain.Entities;

/// <summary>
/// Hand-written fake of <see cref="IPackageRepository"/> (DEC-037). No EF Core is
/// involved; in-place updates are visible through the shared reference-tracking store.
/// </summary>
public sealed class FakePackageRepository : IPackageRepository
{
    private readonly FakeSalsCaterSuiteDataStore _store;

    public FakePackageRepository(FakeSalsCaterSuiteDataStore store)
    {
        _store = store;
    }

    public Task<Package?> GetByIdAsync(int packageId, CancellationToken cancellationToken = default)
    {
        // Deliberately NOT filtered by IsDeleted - it mirrors the repository's entity fetch,
        // which the booking path uses to resolve the package's rate per head (BR-04, DEC-074).
        return Task.FromResult(_store.Packages.FirstOrDefault(p => p.PackageId == packageId));
    }

    public Task<List<PackageDto>> GetProjectionListAsync(bool? activeOnly, CancellationToken cancellationToken = default)
    {
        // The catalogue's scoped population (DEC-074), mirroring the repository's query.
        IEnumerable<Package> packages = _store.Packages.Where(p => !p.IsDeleted);

        if (activeOnly == true)
        {
            packages = packages.Where(p => p.IsActive);
        }

        var result = packages
            .OrderBy(p => p.PackageType).ThenBy(p => p.Description)
            .Select(Map)
            .ToList();

        return Task.FromResult(result);
    }

    public Task<PackageDto?> GetProjectionByIdAsync(int packageId, CancellationToken cancellationToken = default)
    {
        // The same scoped population as the list (DEC-074).
        var package = _store.Packages.FirstOrDefault(p => p.PackageId == packageId && !p.IsDeleted);
        return Task.FromResult(package is null ? null : Map(package));
    }

    public Task AddAsync(Package package, CancellationToken cancellationToken = default)
    {
        _store.Packages.Add(package);
        return Task.CompletedTask;
    }

    /// <summary>
    /// The catalogue aggregate (spec §3.2, DEC-068, KPI-25..KPI-28).
    /// <para>
    /// The mirror of the EF query: the soft-deleted tiers are excluded, the average is divided
    /// from the exact sum and the exact count and rounded away from the midpoint (DEC-003), and
    /// an empty population publishes <see langword="null"/> — never zero.
    /// </para>
    /// </summary>
    public Task<PackageCatalogueSummaryDto> GetCatalogueSummaryAsync(CancellationToken cancellationToken = default)
    {
        var population = _store.Packages.Where(p => !p.IsDeleted).ToList();
        var rates = population.Where(p => p.AmountPax.HasValue).Select(p => p.AmountPax!.Value).ToList();
        var floors = population.Where(p => p.MinPax.HasValue).Select(p => p.MinPax!.Value).ToList();

        var totalPackages = population.Count;
        var activePackages = population.Count(p => p.IsActive);

        var summary = new PackageCatalogueSummaryDto
        {
            TotalPackages = totalPackages,
            ActivePackages = activePackages,
            InactivePackages = totalPackages - activePackages,
            PricedPackages = rates.Count,
            AverageAmountPax = rates.Count > 0
                ? Math.Round(rates.Sum() / rates.Count, 2, MidpointRounding.AwayFromZero)
                : null,
            LowestAmountPax = rates.Count > 0 ? rates.Min() : null,
            HighestAmountPax = rates.Count > 0 ? rates.Max() : null,
            LowestMinPax = floors.Count > 0 ? floors.Min() : null,
            HighestMinPax = floors.Count > 0 ? floors.Max() : null
        };

        return Task.FromResult(summary);
    }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        // Mirror the database: IDENTITY package ids are assigned on INSERT.
        foreach (var package in _store.Packages.Where(p => p.PackageId == 0).ToList())
        {
            _store.AssignPackageId(package);
        }

        return Task.FromResult(1);
    }

    private static PackageDto Map(Package package) => new()
    {
        PackageId = package.PackageId,
        PackageType = package.PackageType ?? string.Empty,
        Description = package.Description ?? string.Empty,
        AmountPax = package.AmountPax,
        MinPax = package.MinPax,
        IsActive = package.IsActive
    };
}
