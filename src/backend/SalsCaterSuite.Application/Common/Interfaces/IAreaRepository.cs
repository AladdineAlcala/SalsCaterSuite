namespace SalsCaterSuite.Application.Common.Interfaces;

using SalsCaterSuite.Application.Features.Packages.DTOs;
using SalsCaterSuite.Domain.Entities;

/// <summary>
/// The municipality catalogue persistence (DEC-037, DEC-077). Expressed in terms of the
/// <see cref="Area"/> domain entity and Application projections only — no EF Core type leaks
/// through this contract.
/// <para>
/// Atomicity: every repository implementation shares the request-scoped
/// <c>SalsCaterSuiteDbContext</c>, so a single <c>SaveChangesAsync</c> commits the entire
/// pending change set.
/// </para>
/// </summary>
public interface IAreaRepository
{
    /// <summary>The municipality catalogue — the live municipalities, ordered by name.</summary>
    Task<List<AreaDto>> GetCatalogueAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// A single <b>live</b> municipality. A soft-deleted municipality does not exist for the
    /// workbench (spec §3.6).
    /// </summary>
    Task<Area?> GetLiveByIdAsync(int areaId, CancellationToken cancellationToken = default);

    /// <summary>True when a live municipality with this id exists.</summary>
    Task<bool> ExistsAsync(int areaId, CancellationToken cancellationToken = default);

    Task AddAsync(Area area, CancellationToken cancellationToken = default);

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
