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
        return await _context.Packages
            .FirstOrDefaultAsync(p => p.PackageId == packageId, cancellationToken);
    }

    public async Task<List<PackageDto>> GetProjectionListAsync(bool? activeOnly, CancellationToken cancellationToken = default)
    {
        var query = _context.Packages.AsNoTracking();

        if (activeOnly == true)
            query = query.Where(p => p.IsActive);

        return await query
            .OrderBy(p => p.PackageType).ThenBy(p => p.Description)
            .Select(p => new PackageDto
            {
                PackageId = p.PackageId,
                PackageType = p.PackageType ?? string.Empty,
                Description = p.Description ?? string.Empty,
                AmountPax = p.AmountPax,
                MinPax = p.MinPax,
                IsActive = p.IsActive
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<PackageDto?> GetProjectionByIdAsync(int packageId, CancellationToken cancellationToken = default)
    {
        return await _context.Packages
            .AsNoTracking()
            .Where(p => p.PackageId == packageId)
            .Select(p => new PackageDto
            {
                PackageId = p.PackageId,
                PackageType = p.PackageType ?? string.Empty,
                Description = p.Description ?? string.Empty,
                AmountPax = p.AmountPax,
                MinPax = p.MinPax,
                IsActive = p.IsActive
            })
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task AddAsync(Package package, CancellationToken cancellationToken = default)
    {
        await _context.Packages.AddAsync(package, cancellationToken);
    }

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return await _context.SaveChangesAsync(cancellationToken);
    }
}
