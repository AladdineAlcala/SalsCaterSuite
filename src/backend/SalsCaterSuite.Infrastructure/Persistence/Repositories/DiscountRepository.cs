namespace SalsCaterSuite.Infrastructure.Persistence.Repositories;

using Microsoft.EntityFrameworkCore;
using SalsCaterSuite.Application.Common.Interfaces;
using SalsCaterSuite.Domain.Entities;

/// <summary>
/// EF Core implementation of the discount-catalogue repository (DEC-037, DEC-114).
/// <para>
/// The catalogue is read by <b>code</b>, never by id: the client sends the code the customer
/// quoted and the server resolves it (BR-02). <c>disc_code</c> carries <b>no unique index</b>,
/// so the query deliberately returns <i>every</i> live match — the Application layer decides
/// that an unknown code and an ambiguous code are both a <c>400</c>, and it never picks one
/// silently.
/// </para>
/// </summary>
public class DiscountRepository : IDiscountRepository
{
    private readonly SalsCaterSuiteDbContext _context;

    public DiscountRepository(SalsCaterSuiteDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// The live (active, not soft-deleted) discounts carrying the given code.
    /// </summary>
    public async Task<IReadOnlyList<Discount>> GetActiveByCodeAsync(
        string discountCode,
        CancellationToken cancellationToken = default)
    {
        var code = discountCode?.Trim() ?? string.Empty;

        if (code.Length == 0)
        {
            return Array.Empty<Discount>();
        }

        return await _context.Discounts
            .AsNoTracking()
            .Where(discount =>
                !discount.IsDeleted &&
                discount.IsActive &&
                discount.DiscountCode == code)
            .OrderBy(discount => discount.DiscountId)
            .ToListAsync(cancellationToken);
    }
}
