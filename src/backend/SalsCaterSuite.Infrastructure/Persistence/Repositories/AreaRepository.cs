namespace SalsCaterSuite.Infrastructure.Persistence.Repositories;

using Microsoft.EntityFrameworkCore;
using SalsCaterSuite.Application.Common.Interfaces;
using SalsCaterSuite.Application.Features.Packages.DTOs.Request;
using SalsCaterSuite.Application.Features.Packages.DTOs.Response;
using SalsCaterSuite.Application.Features.Packages.Models;
using SalsCaterSuite.Domain.Entities;

/// <summary>
/// EF Core implementation of the municipality catalogue repository (DEC-037).
/// </summary>
public class AreaRepository : IAreaRepository
{
    private readonly SalsCaterSuiteDbContext _context;

    public AreaRepository(SalsCaterSuiteDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// The municipality catalogue — the live municipalities, ordered by name. Soft-deleted
    /// municipalities are excluded: the catalogue is the population (contract §13.4).
    /// </summary>
    public async Task<List<AreaResponse>> GetCatalogueAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Areas
            .AsNoTracking()
            .Where(area => !area.IsDeleted)
            .OrderBy(area => area.AreaDetails)
            .Select(area => new AreaResponse
            {
                AreaId = area.AreaId,
                AreaDetails = area.AreaDetails ?? string.Empty,
                AdditionalRate = area.AdditionalRate,
                DistanceKm = area.DistanceKm,
                LogisticsNote = area.LogisticsNote,
                LogisticsCost = area.LogisticsCost
            })
            .ToListAsync(cancellationToken);
    }

    /// <summary>A single live municipality. A soft-deleted one does not exist (spec §3.6).</summary>
    public async Task<Area?> GetLiveByIdAsync(int areaId, CancellationToken cancellationToken = default)
    {
        return await _context.Areas
            .FirstOrDefaultAsync(area => area.AreaId == areaId && !area.IsDeleted, cancellationToken);
    }

    /// <summary>True when a live municipality with this id exists.</summary>
    public async Task<bool> ExistsAsync(int areaId, CancellationToken cancellationToken = default)
    {
        return await _context.Areas
            .AnyAsync(area => area.AreaId == areaId && !area.IsDeleted, cancellationToken);
    }

    public async Task AddAsync(Area area, CancellationToken cancellationToken = default)
    {
        await _context.Areas.AddAsync(area, cancellationToken);
    }

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return await _context.SaveChangesAsync(cancellationToken);
    }
}
