namespace SalsCaterSuite.Application.Common.Interfaces;

using SalsCaterSuite.Domain.Entities;

/// <summary>
/// The read-only applicable-pax classification lookup (REQ-PKG-04, DEC-173).
/// <para>
/// The five rows are fixed reference data seeded by the schema migration; the lookup exposes
/// reads only — there is no administration surface and no create, update or delete contract.
/// </para>
/// <para>
/// The contract is expressed in Domain entities and Application DTOs only: no
/// <c>DbSet</c>, no <c>IQueryable</c> and no <c>DbContext</c> type appears here (DEC-037).
/// </para>
/// </summary>
public interface IPackagePaxApplicableRepository
{
    /// <summary>
    /// Every live (not soft-deleted) classification, ordered by its identifier.
    /// </summary>
    Task<IReadOnlyList<PackagePaxApplicable>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Whether a live classification carries this identifier.
    /// <para>
    /// The package write commands use it to reject a reference to a row that does not exist —
    /// the referential half of the DEC-173 validation, which cannot live in the synchronous
    /// validator.
    /// </para>
    /// </summary>
    Task<bool> ExistsAsync(int numPaxId, CancellationToken cancellationToken = default);
}
