namespace SalsCaterSuite.Application.Common.Interfaces;

using SalsCaterSuite.Application.Features.Packages.DTOs;
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

    /// <summary>Package list projection, optionally restricted to active packages.</summary>
    Task<List<PackageDto>> GetProjectionListAsync(bool? activeOnly, CancellationToken cancellationToken = default);

    /// <summary>Single-package projection.</summary>
    Task<PackageDto?> GetProjectionByIdAsync(int packageId, CancellationToken cancellationToken = default);

    Task AddAsync(Package package, CancellationToken cancellationToken = default);

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
