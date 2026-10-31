namespace SalsCaterSuite.Application.Common.Interfaces;

using SalsCaterSuite.Application.Features.Packages.DTOs.Request;
using SalsCaterSuite.Application.Features.Packages.DTOs.Response;
using SalsCaterSuite.Application.Features.Packages.Models;
using SalsCaterSuite.Domain.Entities;

/// <summary>
/// Package aggregate persistence (DEC-037). Expressed in terms of the <see cref="Package"/>
/// domain entity and Application projections only — no EF Core type leaks through this
/// contract.
/// <para>
/// Atomicity: every repository implementation shares the request-scoped
/// <c>SalsCaterSuiteDbContext</c>, so a single <see cref="SaveChangesAsync"/> commits the
/// entire pending change set.
/// </para>
/// </summary>
public interface IPackageRepository
{
    Task<Package?> GetByIdAsync(int packageId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Every package code the catalogue holds, including the soft-deleted rows, so the code
    /// generator never reuses a number (the owner's 2026-10-24 request).
    /// </summary>
    Task<IReadOnlyList<string>> GetPackageCodesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Package list projection, optionally restricted to active packages and, when
    /// <paramref name="areaId"/> is present, to the tiers whose <b>active</b> coverage includes
    /// that municipality (contract §12.1, DEC-157).
    /// </summary>
    Task<List<PackageResponse>> GetProjectionListAsync(
        bool? activeOnly,
        int? areaId = null,
        CancellationToken cancellationToken = default);

    /// <summary>Single-package projection.</summary>
    Task<PackageResponse?> GetProjectionByIdAsync(int packageId, CancellationToken cancellationToken = default);

    /// <summary>
    /// The catalogue aggregate (spec §3.2, DEC-068, KPI-25..KPI-28) — the counts, the rate
    /// average and range and the headcount-floor range of the catalogue.
    /// <para>
    /// The soft-deleted tiers are excluded by the query itself, exactly as the dashboard's
    /// queries exclude them (DEC-045 / DEC-050 — there is no global query filter).
    /// </para>
    /// </summary>
    Task<PackageCatalogueSummaryResponse> GetCatalogueSummaryAsync(CancellationToken cancellationToken = default);

    Task AddAsync(Package package, CancellationToken cancellationToken = default);

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
