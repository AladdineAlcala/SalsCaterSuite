namespace SalsCaterSuite.Application.Common.Interfaces;

using SalsCaterSuite.Domain.Entities;

/// <summary>
/// The add-on master's persistence (REQ-BKG-07, DEC-142, DEC-158). Expressed in terms of the
/// <see cref="AddonDetail"/> and <see cref="AddonCategory"/> domain entities only — no EF Core
/// type leaks through this contract (DEC-037).
/// <para>
/// The booking picker is read-only; the administration CRUD added on 2026-10-24 applies the
/// soft-delete convention and shares the request-scoped context with every other repository.
/// </para>
/// </summary>
public interface IAddonRepository
{
    /// <summary>The category master, ordered by name. Soft-deleted categories are excluded.</summary>
    Task<IReadOnlyList<AddonCategory>> GetCategoriesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// The <b>active</b> add-ons, optionally narrowed to one category, with the category row
    /// loaded. Soft-deleted rows are excluded (contract §16.1).
    /// </summary>
    Task<IReadOnlyList<AddonDetail>> GetActiveWithCategoryAsync(
        int? categoryId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The add-ons of the given ids that are <b>not soft-deleted</b>, with the category row
    /// loaded. Whether a row is <b>active</b> is the use case's decision, because the two
    /// failures carry different messages (contract §16.4).
    /// </summary>
    Task<IReadOnlyList<AddonDetail>> GetLiveByIdsAsync(
        IEnumerable<int> addonIds,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The administration list: every <b>non-soft-deleted</b> add-on, active or inactive, with the
    /// category and department rows loaded, ordered by description (the 2026-10-24 request).
    /// </summary>
    Task<IReadOnlyList<AddonDetail>> GetAllForManagementAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// One non-soft-deleted add-on with its category and department loaded. Tracked, so the
    /// update and delete commands can mutate it; a soft-deleted row answers <c>null</c>.
    /// </summary>
    Task<AddonDetail?> GetByIdAsync(int addonId, CancellationToken cancellationToken = default);

    /// <summary>Whether a live (non-soft-deleted) category carries this id.</summary>
    Task<bool> CategoryExistsAsync(int addonCategoryId, CancellationToken cancellationToken = default);

    Task AddAsync(AddonDetail addon, CancellationToken cancellationToken = default);

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
