namespace SalsCaterSuite.UnitTests.Common.Fakes;

using SalsCaterSuite.Application.Common.Interfaces;
using SalsCaterSuite.Domain.Entities;

/// <summary>
/// Hand-written fake of <see cref="IAddonRepository"/> (DEC-037). It mirrors the EF
/// implementation's scoping exactly — soft-deleted rows are excluded, the master is read-only,
/// and the master ships <b>empty</b> unless a test seeds it (BR-13, RSK-066).
/// </summary>
public sealed class FakeAddonRepository : IAddonRepository
{
    private readonly FakeSalsCaterSuiteDataStore _store;

    public FakeAddonRepository(FakeSalsCaterSuiteDataStore store)
    {
        _store = store;
    }

    public Task<IReadOnlyList<AddonCategory>> GetCategoriesAsync(
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<AddonCategory> result = _store.AddonCategories
            .Where(category => !category.IsDeleted)
            .OrderBy(category => category.AddonCategoryName)
            .ThenBy(category => category.AddonCategoryId)
            .ToList();

        return Task.FromResult(result);
    }

    public Task<IReadOnlyList<AddonDetail>> GetActiveWithCategoryAsync(
        int? categoryId,
        CancellationToken cancellationToken = default)
    {
        IEnumerable<AddonDetail> rows = _store.AddonDetails
            .Where(addon => addon.IsActive && !addon.IsDeleted);

        if (categoryId.HasValue)
        {
            rows = rows.Where(addon => addon.AddonCategoryId == categoryId.Value);
        }

        IReadOnlyList<AddonDetail> result = rows
            .OrderBy(addon => addon.Description)
            .ThenBy(addon => addon.AddonId)
            .ToList();

        foreach (var addon in result)
        {
            HydrateCategory(addon);
        }

        return Task.FromResult(result);
    }

    public Task<IReadOnlyList<AddonDetail>> GetLiveByIdsAsync(
        IEnumerable<int> addonIds,
        CancellationToken cancellationToken = default)
    {
        var ids = addonIds.Distinct().ToHashSet();

        IReadOnlyList<AddonDetail> result = _store.AddonDetails
            .Where(addon => ids.Contains(addon.AddonId) && !addon.IsDeleted)
            .ToList();

        foreach (var addon in result)
        {
            HydrateCategory(addon);
        }

        return Task.FromResult(result);
    }

    private void HydrateCategory(AddonDetail addon)
    {
        if (addon.AddonCategoryId.HasValue)
        {
            addon.AddonCategory ??= _store.AddonCategories
                .FirstOrDefault(category => category.AddonCategoryId == addon.AddonCategoryId.Value);
        }

        if (addon.DeptId.HasValue)
        {
            addon.Department ??= _store.Departments
                .FirstOrDefault(department => department.DeptId == addon.DeptId.Value);
        }
    }

    public Task<IReadOnlyList<AddonDetail>> GetAllForManagementAsync(
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<AddonDetail> result = _store.AddonDetails
            .Where(addon => !addon.IsDeleted)
            .OrderBy(addon => addon.Description)
            .ThenBy(addon => addon.AddonId)
            .ToList();

        foreach (var addon in result)
        {
            HydrateCategory(addon);
        }

        return Task.FromResult(result);
    }

    public Task<AddonDetail?> GetByIdAsync(
        int addonId,
        CancellationToken cancellationToken = default)
    {
        var addon = _store.AddonDetails
            .FirstOrDefault(row => row.AddonId == addonId && !row.IsDeleted);

        if (addon is not null)
        {
            HydrateCategory(addon);
        }

        return Task.FromResult(addon);
    }

    public Task<bool> CategoryExistsAsync(
        int addonCategoryId,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_store.AddonCategories.Any(
            category => category.AddonCategoryId == addonCategoryId && !category.IsDeleted));
    }

    public Task AddAsync(AddonDetail addon, CancellationToken cancellationToken = default)
    {
        _store.AddonDetails.Add(addon);
        return Task.CompletedTask;
    }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        foreach (var addon in _store.AddonDetails.Where(row => row.AddonId == 0).ToList())
        {
            _store.AssignAddonId(addon);
        }

        return Task.FromResult(1);
    }
}
