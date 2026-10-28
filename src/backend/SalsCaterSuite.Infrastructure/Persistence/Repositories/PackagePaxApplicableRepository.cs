namespace SalsCaterSuite.Infrastructure.Persistence.Repositories;

using Microsoft.EntityFrameworkCore;
using SalsCaterSuite.Application.Common.Interfaces;
using SalsCaterSuite.Domain.Entities;

/// <summary>
/// EF Core implementation of the read-only applicable-pax classification lookup
/// (REQ-PKG-04, DEC-173).
/// <para>
/// The five rows are fixed reference data seeded by the schema migration. The repository is
/// read-only: it exposes no write operation, because the lookup has no administration surface.
/// </para>
/// <para>
/// This is the only layer that touches EF Core (DEC-037); the Application contract speaks in
/// Domain entities only.
/// </para>
/// </summary>
public class PackagePaxApplicableRepository : IPackagePaxApplicableRepository
{
    private readonly SalsCaterSuiteDbContext _context;

    public PackagePaxApplicableRepository(SalsCaterSuiteDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<PackagePaxApplicable>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        // The live population only, ordered by the fixed identifier so the endpoint publishes
        // the options in ascending order without the client re-sorting them.
        return await _context.PackagePaxApplicables
            .AsNoTracking()
            .Where(option => !option.IsDeleted)
            .OrderBy(option => option.NumPaxId)
            .ToListAsync(cancellationToken);
    }

    public Task<bool> ExistsAsync(int numPaxId, CancellationToken cancellationToken = default)
    {
        // A soft-deleted option does not count: it is no longer a selectable classification.
        return _context.PackagePaxApplicables
            .AsNoTracking()
            .AnyAsync(
                option => option.NumPaxId == numPaxId && !option.IsDeleted,
                cancellationToken);
    }
}
