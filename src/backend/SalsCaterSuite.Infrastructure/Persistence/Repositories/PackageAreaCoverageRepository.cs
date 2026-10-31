namespace SalsCaterSuite.Infrastructure.Persistence.Repositories;

using Microsoft.EntityFrameworkCore;
using SalsCaterSuite.Application.Common.Interfaces;
using SalsCaterSuite.Application.Features.Packages.DTOs.Request;
using SalsCaterSuite.Application.Features.Packages.DTOs.Response;
using SalsCaterSuite.Application.Features.Packages.Models;
using SalsCaterSuite.Domain.Entities;

/// <summary>
/// EF Core implementation of the coverage repository (DEC-037). The projection resolves the
/// municipality's details in one round trip, so the coverage matrix read is a single statement.
/// </summary>
public class PackageAreaCoverageRepository : IPackageAreaCoverageRepository
{
    private readonly SalsCaterSuiteDbContext _context;

    public PackageAreaCoverageRepository(SalsCaterSuiteDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// The tier's live coverage rows, with the municipality's details resolved.
    /// <para>
    /// A coverage row whose <b>municipality</b> is soft-deleted is excluded: the municipality
    /// catalogue is the population (contract §13.4). The <c>where</c> after the left join turns
    /// it into an inner join, which is exactly the exclusion the contract requires.
    /// </para>
    /// </summary>
    public async Task<IReadOnlyList<PackageAreaCoverageProjection>> GetLiveCoveragesAsync(
        int packageId,
        CancellationToken cancellationToken = default)
    {
        var query =
            from coverage in _context.PackageAreaCoverages.AsNoTracking()
            where coverage.PackageId == packageId && !coverage.IsDeleted
            join area in _context.Areas on coverage.AreaId equals area.AreaId into areas
            from area in areas.DefaultIfEmpty()
            where area != null && !area.IsDeleted
            select new PackageAreaCoverageProjection
            {
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

        var result = await query.ToListAsync(cancellationToken);

        return result;
    }

    /// <summary>
    /// Every coverage row the tier holds, the soft-deleted ones included — the population the
    /// write use case matches the natural key over, so a re-added municipality resurrects its row
    /// (DEC-088).
    /// </summary>
    public async Task<IReadOnlyList<PackageAreaCoverage>> GetAllCoveragesAsync(
        int packageId,
        CancellationToken cancellationToken = default)
    {
        return await _context.PackageAreaCoverages
            .Where(coverage => coverage.PackageId == packageId)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(PackageAreaCoverage coverage, CancellationToken cancellationToken = default)
    {
        await _context.PackageAreaCoverages.AddAsync(coverage, cancellationToken);
    }

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return await _context.SaveChangesAsync(cancellationToken);
    }
}
