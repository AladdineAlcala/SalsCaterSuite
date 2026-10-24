namespace SalsCaterSuite.Application.Common.Interfaces;

using SalsCaterSuite.Domain.Entities;

/// <summary>
/// The department master's read contract (the owner's 2026-10-24 request). The add-on form needs
/// the list to populate its optional department selector; the master itself has no administration
/// surface in this cycle, so this contract exposes reads only (BR-13 — no department is seeded).
/// </summary>
public interface IDepartmentRepository
{
    /// <summary>Every non-soft-deleted department, ordered by name.</summary>
    Task<IReadOnlyList<Department>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>Whether a live department carries this id.</summary>
    Task<bool> ExistsAsync(int deptId, CancellationToken cancellationToken = default);
}
