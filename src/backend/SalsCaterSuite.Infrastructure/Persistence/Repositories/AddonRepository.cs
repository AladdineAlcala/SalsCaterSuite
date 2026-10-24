namespace SalsCaterSuite.Infrastructure.Persistence.Repositories;

using Microsoft.EntityFrameworkCore;
using SalsCaterSuite.Application.Common.Interfaces;
using SalsCaterSuite.Domain.Entities;

/// <summary>
/// EF Core implementation of the read-only add-on master (REQ-BKG-07, DEC-037, DEC-142).
/// The soft-delete exclusions are applied by these queries — no global query filter is
/// introduced (DEC-038, DEC-045).
/// </summary>
public class AddonRepository : IAddonRepository
{
    private readonly SalsCaterSuiteDbContext _context;

    public AddonRepository(SalsCaterSuiteDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<AddonCategory>> GetCategoriesAsync(
        CancellationToken cancellationToken = default)
    {
        return await _context.AddonCategories
            .AsNoTracking()
            .Where(category => !category.IsDeleted)
            .OrderBy(category => category.AddonCategoryName)
            .ThenBy(category => category.AddonCategoryId)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AddonDetail>> GetActiveWithCategoryAsync(
        int? categoryId,
        CancellationToken cancellationToken = default)
    {
        var query = _context.AddonDetails
            .AsNoTracking()
            .Include(addon => addon.AddonCategory)
            .Include(addon => addon.Department)
            .Where(addon => addon.IsActive && !addon.IsDeleted);

        if (categoryId.HasValue)
        {
            query = query.Where(addon => addon.AddonCategoryId == categoryId.Value);
        }

        return await query
            .OrderBy(addon => addon.Description)
            .ThenBy(addon => addon.AddonId)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AddonDetail>> GetLiveByIdsAsync(
        IEnumerable<int> addonIds,
        CancellationToken cancellationToken = default)
    {
        var ids = addonIds.Distinct().ToList();

        if (ids.Count == 0)
        {
            return Array.Empty<AddonDetail>();
        }

        return await _context.AddonDetails
            .Include(addon => addon.AddonCategory)
            .Include(addon => addon.Department)
            .Where(addon => ids.Contains(addon.AddonId) && !addon.IsDeleted)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AddonDetail>> GetAllForManagementAsync(
        CancellationToken cancellationToken = default)
    {
        return await _context.AddonDetails
            .AsNoTracking()
            .Include(addon => addon.AddonCategory)
            .Include(addon => addon.Department)
            .Where(addon => !addon.IsDeleted)
            .OrderBy(addon => addon.Description)
            .ThenBy(addon => addon.AddonId)
            .ToListAsync(cancellationToken);
    }

    public async Task<AddonDetail?> GetByIdAsync(
        int addonId,
        CancellationToken cancellationToken = default)
    {
        // Tracked deliberately: the update and delete commands mutate this row and share the
        // request-scoped context. A soft-deleted row is not found.
        return await _context.AddonDetails
            .Include(addon => addon.AddonCategory)
            .Include(addon => addon.Department)
            .FirstOrDefaultAsync(
                addon => addon.AddonId == addonId && !addon.IsDeleted,
                cancellationToken);
    }

    public Task<bool> CategoryExistsAsync(
        int addonCategoryId,
        CancellationToken cancellationToken = default)
    {
        return _context.AddonCategories
            .AsNoTracking()
            .AnyAsync(
                category => category.AddonCategoryId == addonCategoryId && !category.IsDeleted,
                cancellationToken);
    }

    public async Task AddAsync(AddonDetail addon, CancellationToken cancellationToken = default)
    {
        await _context.AddonDetails.AddAsync(addon, cancellationToken);
    }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return _context.SaveChangesAsync(cancellationToken);
    }
}
