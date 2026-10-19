namespace SalsCaterSuite.Infrastructure.Persistence.Repositories;

using SalsCaterSuite.Application.Common.Interfaces;
using SalsCaterSuite.Domain.Entities;

/// <summary>
/// EF Core implementation of the refund repository (DEC-037, DEC-024). Refunds are
/// staged here and committed by <see cref="BookingRepository.SaveChangesAsync"/>
/// because they belong to the booking aggregate.
/// </summary>
public class RefundRepository : IRefundRepository
{
    private readonly SalsCaterSuiteDbContext _context;

    public RefundRepository(SalsCaterSuiteDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(Refund refund, CancellationToken cancellationToken = default)
    {
        await _context.Refunds.AddAsync(refund, cancellationToken);
    }
}
