namespace SalsCaterSuite.Application.Common.Interfaces;

using SalsCaterSuite.Domain.Entities;

/// <summary>
/// The discount-catalogue persistence (DEC-037, DEC-114).
/// <para>
/// The catalogue is read by <b>code</b>, never by id: the client sends the code the customer
/// quoted and the server resolves it (BR-02). <c>disc_code</c> carries <b>no unique index</b>,
/// so the resolution is defined explicitly by the Application layer — no active discount with
/// the code is a <c>400</c>, and more than one is a <c>400</c> as well. The server never picks
/// one silently.
/// </para>
/// <para>
/// Atomicity: every repository implementation shares the request-scoped
/// <c>SalsCaterSuiteDbContext</c>, so a single <see cref="SaveChangesAsync"/> commits the
/// entire pending change set.
/// </para>
/// </summary>
public interface IDiscountRepository
{
    /// <summary>
    /// The <b>live</b> (active, not soft-deleted) discounts carrying the given code. The
    /// catalogue is empty today (BR-13 forbids seeding business data), so an unknown code
    /// resolves to an empty list and the caller answers <c>400</c>.
    /// </summary>
    Task<IReadOnlyList<Discount>> GetActiveByCodeAsync(
        string discountCode,
        CancellationToken cancellationToken = default);
}
