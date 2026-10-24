namespace SalsCaterSuite.Infrastructure.Persistence.Repositories;

using Microsoft.EntityFrameworkCore;
using SalsCaterSuite.Application.Common.Interfaces;
using SalsCaterSuite.Application.Features.Packages.DTOs;
using SalsCaterSuite.Domain.Entities;

/// <summary>
/// EF Core implementation of the package repository (DEC-037). The projections are
/// byte-for-byte the ones previously inlined in the Packages feature use cases.
/// </summary>
public class PackageRepository : IPackageRepository
{
    private readonly SalsCaterSuiteDbContext _context;

    public PackageRepository(SalsCaterSuiteDbContext context)
    {
        _context = context;
    }

    public async Task<Package?> GetByIdAsync(int packageId, CancellationToken cancellationToken = default)
    {
        // Deliberately NOT filtered by IsDeleted: this is the entity fetch the write commands
        // and CreateBookingCommand use, and the booking path resolves the package's rate per
        // head from it (BR-04). Scoping the soft-delete filter to it would silently change
        // booking creation, which needs its own decision (DEC-074).
        return await _context.Packages
            .FirstOrDefaultAsync(p => p.PackageId == packageId, cancellationToken);
    }

    public async Task<IReadOnlyList<string>> GetPackageCodesAsync(
        CancellationToken cancellationToken = default)
    {
        // Deliberately includes the soft-deleted rows: a code is a business identifier and must
        // never be reused, even after the tier it belonged to is archived.
        return await _context.Packages
            .AsNoTracking()
            .Select(p => p.PackageCode)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<PackageDto>> GetProjectionListAsync(
        bool? activeOnly,
        int? areaId = null,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Packages.AsNoTracking();

        // The catalogue's population (DEC-074). There is no global query filter (DEC-038), so
        // the catalogue scopes it here, exactly as the dashboard and the Customer Master do
        // (DEC-045 / DEC-050). Without it the list and the summary would describe two
        // different catalogues.
        query = query.Where(p => !p.IsDeleted);

        if (activeOnly == true)
            query = query.Where(p => p.IsActive);

        // DEC-157: the owner's "Packages will display based on the selected area" rule. Only a
        // tier whose LIVE coverage includes the municipality qualifies, so a removed coverage
        // (soft-deleted) never keeps a tier in the list.
        if (areaId.HasValue)
        {
            query = query.Where(p => p.AreaCoverages.Any(coverage =>
                coverage.AreaId == areaId.Value &&
                coverage.IsActive &&
                !coverage.IsDeleted));
        }

        return await query
            .OrderBy(p => p.PackageType).ThenBy(p => p.Description)
            .Select(p => new PackageDto
            {
                PackageId = p.PackageId,
                PackageCode = p.PackageCode ?? string.Empty,
                PackageType = p.PackageType ?? string.Empty,
                Description = p.Description ?? string.Empty,
                AmountPax = p.AmountPax,
                MinPax = p.MinPax,
                ExtendedLocationAmount = p.ExtendedLocationAmount,
                IsActive = p.IsActive
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<PackageDto?> GetProjectionByIdAsync(int packageId, CancellationToken cancellationToken = default)
    {
        // The same scoped population as the list (DEC-074): a soft-deleted tier is not part of
        // the catalogue and answers 404 on the detail endpoint.
        return await _context.Packages
            .AsNoTracking()
            .Where(p => p.PackageId == packageId && !p.IsDeleted)
            .Select(p => new PackageDto
            {
                PackageId = p.PackageId,
                PackageCode = p.PackageCode ?? string.Empty,
                PackageType = p.PackageType ?? string.Empty,
                Description = p.Description ?? string.Empty,
                AmountPax = p.AmountPax,
                MinPax = p.MinPax,
                ExtendedLocationAmount = p.ExtendedLocationAmount,
                IsActive = p.IsActive
            })
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task AddAsync(Package package, CancellationToken cancellationToken = default)
    {
        await _context.Packages.AddAsync(package, cancellationToken);
    }

    /// <summary>
    /// The catalogue aggregate (spec §3.2, DEC-068, KPI-25..KPI-28).
    /// <para>
    /// The whole aggregate is one round trip: grouping by a constant produces a single group, so
    /// the counts, the rate sum and the four extremes are measured by the database in one
    /// statement (the documented EF Core pattern for an aggregate over an entire query).
    /// </para>
    /// <para>
    /// The soft-deleted tiers are excluded by the query itself, exactly as the dashboard's
    /// queries exclude them (DEC-045 / DEC-050 — there is no global query filter).
    /// </para>
    /// </summary>
    public async Task<PackageCatalogueSummaryDto> GetCatalogueSummaryAsync(CancellationToken cancellationToken = default)
    {
        var population = _context.Packages
            .AsNoTracking()
            .Where(p => !p.IsDeleted);

        // An empty catalogue produces no group at all, so the list comes back empty.
        // The result is materialised rather than read with FirstOrDefault: a single-row
        // operator over a grouped query has no deterministic order, which EF Core warns about
        // on every call — and the group is unique by construction, so there is nothing to order.
        var groups = await population
            .GroupBy(_ => 1)
            .Select(g => new
            {
                TotalPackages = g.Count(),
                ActivePackages = g.Count(p => p.IsActive),
                PricedPackages = g.Count(p => p.AmountPax != null),
                SumAmountPax = g.Sum(p => p.AmountPax),
                LowestAmountPax = g.Min(p => p.AmountPax),
                HighestAmountPax = g.Max(p => p.AmountPax),
                LowestMinPax = g.Min(p => p.MinPax),
                HighestMinPax = g.Max(p => p.MinPax)
            })
            .ToListAsync(cancellationToken);

        if (groups.Count == 0)
        {
            // An empty catalogue: the counts are real measurements and are zero; the rate and
            // the floor aggregates are unknown and stay null (contract §9.2 rule 6).
            return new PackageCatalogueSummaryDto
            {
                TotalPackages = 0,
                ActivePackages = 0,
                InactivePackages = 0,
                PricedPackages = 0
            };
        }

        var aggregate = groups[0];

        return new PackageCatalogueSummaryDto
        {
            TotalPackages = aggregate.TotalPackages,
            ActivePackages = aggregate.ActivePackages,
            InactivePackages = aggregate.TotalPackages - aggregate.ActivePackages,
            PricedPackages = aggregate.PricedPackages,
            AverageAmountPax = AverageRate(aggregate.SumAmountPax, aggregate.PricedPackages),
            LowestAmountPax = aggregate.LowestAmountPax,
            HighestAmountPax = aggregate.HighestAmountPax,
            LowestMinPax = aggregate.LowestMinPax,
            HighestMinPax = aggregate.HighestMinPax
        };
    }

    /// <summary>
    /// KPI-26 — the mean of the priced population's rates.
    /// <para>
    /// The mean is divided from the exact sum and the exact count, and it is then rounded to two
    /// decimals <b>away from the midpoint</b> (DEC-003). The database's own <c>AVG</c> is not
    /// used: its result scale is the column's scale, which would round the mean a second time
    /// and could disagree with this rule on a midpoint.
    /// </para>
    /// </summary>
    private static decimal? AverageRate(decimal? sum, int pricedCount)
    {
        if (pricedCount == 0 || sum is null)
        {
            return null;
        }

        return Math.Round(sum.Value / pricedCount, 2, MidpointRounding.AwayFromZero);
    }

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return await _context.SaveChangesAsync(cancellationToken);
    }
}
