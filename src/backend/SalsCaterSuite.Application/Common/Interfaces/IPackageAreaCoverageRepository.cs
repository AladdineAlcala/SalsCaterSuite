namespace SalsCaterSuite.Application.Common.Interfaces;

using SalsCaterSuite.Application.Features.Packages.DTOs.Request;
using SalsCaterSuite.Application.Features.Packages.DTOs.Response;
using SalsCaterSuite.Application.Features.Packages.Models;
using SalsCaterSuite.Domain.Entities;

/// <summary>
/// The coverage-matrix persistence (DEC-037). Expressed in terms of the
/// <see cref="PackageAreaCoverage"/> domain entity and Application projections only — no EF Core
/// type leaks through this contract.
/// <para>
/// Atomicity: every repository implementation shares the request-scoped
/// <c>SalsCaterSuiteDbContext</c>, so a single <c>SaveChangesAsync</c> commits the entire
/// pending change set — a coverage set is replaced as one document (DEC-085).
/// </para>
/// </summary>
public interface IPackageAreaCoverageRepository
{
    /// <summary>
    /// The tier's <b>live</b> coverage rows, with the municipality's details resolved. A coverage
    /// row whose municipality is soft-deleted is excluded from the projection — the municipality
    /// catalogue is the population (contract §13.4).
    /// </summary>
    Task<IReadOnlyList<PackageAreaCoverageProjection>> GetLiveCoveragesAsync(
        int packageId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// <b>Every</b> coverage row the tier holds, including the soft-deleted ones. The write use
    /// case matches the natural key over this population so a re-added municipality
    /// <b>resurrects</b> its soft-deleted row instead of inserting a second one (DEC-088).
    /// </summary>
    Task<IReadOnlyList<PackageAreaCoverage>> GetAllCoveragesAsync(
        int packageId,
        CancellationToken cancellationToken = default);

    Task AddAsync(PackageAreaCoverage coverage, CancellationToken cancellationToken = default);

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
