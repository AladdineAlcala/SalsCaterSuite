namespace SalsCaterSuite.Infrastructure.Persistence.Repositories;

using Microsoft.EntityFrameworkCore;
using SalsCaterSuite.Application.Common.Interfaces;
using SalsCaterSuite.Domain.Entities;

public class BookingRepository : IBookingRepository
{
    private readonly ISalsCaterSuiteDbContext _context;

    public BookingRepository(ISalsCaterSuiteDbContext context)
    {
        _context = context;
    }

    public async Task<Booking?> GetByIdAsync(int transactionId, CancellationToken cancellationToken = default)
    {
        return await _context.Bookings
            .FirstOrDefaultAsync(b => b.TransactionId == transactionId, cancellationToken);
    }

    public async Task<Booking?> GetWithDetailsAsync(int transactionId, CancellationToken cancellationToken = default)
    {
        return await _context.Bookings
            .Include(b => b.Customer)
            .Include(b => b.Package)
            .Include(b => b.Payments)
            .Include(b => b.Refunds)
            .FirstOrDefaultAsync(b => b.TransactionId == transactionId, cancellationToken);
    }

    public async Task<IReadOnlyList<Booking>> GetByDateRangeAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
    {
        return await _context.Bookings
            .Where(b => b.StartDate >= startDate && b.StartDate <= endDate)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(Booking booking, CancellationToken cancellationToken = default)
    {
        await _context.Bookings.AddAsync(booking, cancellationToken);
    }

    public void Update(Booking booking)
    {
        _context.Bookings.Update(booking);
    }

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return await _context.SaveChangesAsync(cancellationToken);
    }
}

