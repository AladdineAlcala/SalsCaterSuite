namespace SalsCaterSuite.Infrastructure.Persistence.Repositories;

using Microsoft.EntityFrameworkCore;
using SalsCaterSuite.Application.Common.Interfaces;
using SalsCaterSuite.Application.Common.Models;
using SalsCaterSuite.Domain.Entities;

/// <summary>EF Core implementation of the payment repository (DEC-037, DEC-012).</summary>
public class PaymentRepository : IPaymentRepository
{
    private readonly SalsCaterSuiteDbContext _context;

    public PaymentRepository(SalsCaterSuiteDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(Payment payment, CancellationToken cancellationToken = default)
    {
        await _context.Payments.AddAsync(payment, cancellationToken);
    }

    public void Remove(Payment payment)
    {
        _context.Payments.Remove(payment);
    }

    public async Task<int> CountCreatedOnDateAsync(DateTime date, CancellationToken cancellationToken = default)
    {
        // Mirrors the original predicate exactly: only payments with a CreatedDate
        // falling on the given calendar day are counted.
        var day = date.Date;
        return await _context.Payments
            .CountAsync(p => p.CreatedDate.HasValue && p.CreatedDate.Value.Date == day, cancellationToken);
    }

    /// <summary>
    /// Sums and counts the payments inside the inclusive range (spec §7 R1). Soft-deleted
    /// payments are excluded; the exclusion is scoped to this dashboard query and no global
    /// query filter is introduced (DEC-038, spec §6.13).
    /// </summary>
    public async Task<PaymentRangeAggregate> GetRangeAggregateAsync(DateTime fromInclusive, DateTime toInclusive, CancellationToken cancellationToken = default)
    {
        var query = _context.Payments
            .AsNoTracking()
            .Where(p =>
                !p.IsDeleted &&
                p.DateOfPayment >= fromInclusive &&
                p.DateOfPayment <= toInclusive);

        // The nullable selector keeps the aggregate nullable so an empty range yields 0
        // rather than a null-propagation failure.
        var totalAmount = await query.SumAsync(p => (decimal?)p.AmountPaid, cancellationToken) ?? 0m;
        var paymentCount = await query.CountAsync(cancellationToken);

        return new PaymentRangeAggregate(totalAmount, paymentCount);
    }

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return await _context.SaveChangesAsync(cancellationToken);
    }
}
