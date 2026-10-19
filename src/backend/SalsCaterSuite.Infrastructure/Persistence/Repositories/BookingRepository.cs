namespace SalsCaterSuite.Infrastructure.Persistence.Repositories;

using Microsoft.EntityFrameworkCore;
using SalsCaterSuite.Application.Common.Interfaces;
using SalsCaterSuite.Domain.Entities;
using SalsCaterSuite.Domain.Enums;

/// <summary>
/// EF Core implementation of the booking aggregate repository (DEC-037).
/// Depends on the concrete <see cref="SalsCaterSuiteDbContext"/>; all EF Core
/// knowledge stays inside the Infrastructure layer.
/// </summary>
public class BookingRepository : IBookingRepository
{
    private readonly SalsCaterSuiteDbContext _context;

    public BookingRepository(SalsCaterSuiteDbContext context)
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

    public async Task<Booking?> GetWithPaymentsAsync(int transactionId, CancellationToken cancellationToken = default)
    {
        return await _context.Bookings
            .Include(b => b.Payments)
            .FirstOrDefaultAsync(b => b.TransactionId == transactionId, cancellationToken);
    }

    public async Task<IReadOnlyList<Booking>> GetByDateRangeAsync(DateOnly fromDate, DateOnly toDate, CancellationToken cancellationToken = default)
    {
        return await _context.Bookings
            .Where(b => b.EventDate >= fromDate && b.EventDate <= toDate)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Booking>> GetForCalendarAsync(DateOnly fromDate, DateOnly toDate, CancellationToken cancellationToken = default)
    {
        return await _context.Bookings
            .AsNoTracking()
            .Include(b => b.Customer)
            .Include(b => b.Package)
            .Include(b => b.Payments)
            .Where(b =>
                b.Status != BookingStatus.Cancelled &&
                b.EventDate >= fromDate &&
                b.EventDate <= toDate)
            .OrderBy(b => b.EventDate)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// The dashboard's period population (spec §7 R2). The soft-delete exclusion is applied
    /// here only — no global query filter is introduced (DEC-038, spec §6.13).
    /// </summary>
    public async Task<IReadOnlyList<Booking>> GetDashboardPeriodBookingsAsync(DateOnly fromDate, DateOnly toDate, CancellationToken cancellationToken = default)
    {
        return await _context.Bookings
            .AsNoTracking()
            .AsSplitQuery()
            .Include(b => b.Customer)
            .Include(b => b.Payments)
            .Where(b =>
                !b.IsDeleted &&
                b.Status != BookingStatus.Cancelled &&
                b.Status != BookingStatus.Expired &&
                b.EventDate >= fromDate &&
                b.EventDate <= toDate)
            .OrderBy(b => b.EventDate)
            .ThenBy(b => b.TransactionId)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// The dashboard's receivables population (spec §7 R3) — every non-cancelled booking.
    /// The soft-delete exclusion is applied here only (DEC-038, spec §6.13).
    /// </summary>
    public async Task<IReadOnlyList<Booking>> GetDashboardReceivablesBookingsAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Bookings
            .AsNoTracking()
            .AsSplitQuery()
            .Include(b => b.Customer)
            .Include(b => b.Payments)
            .Where(b =>
                !b.IsDeleted &&
                b.Status != BookingStatus.Cancelled)
            .OrderBy(b => b.TransactionId)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// The dashboard's upcoming schedule (spec §7 R5). The five-row limit is applied by the
    /// Application layer, not in SQL, so the limit is not silently applied before filtering.
    /// </summary>
    public async Task<IReadOnlyList<Booking>> GetDashboardUpcomingBookingsAsync(DateOnly fromDate, CancellationToken cancellationToken = default)
    {
        return await _context.Bookings
            .AsNoTracking()
            .AsSplitQuery()
            .Include(b => b.Customer)
            .Include(b => b.Payments)
            .Where(b =>
                !b.IsDeleted &&
                b.Status != BookingStatus.Cancelled &&
                b.Status != BookingStatus.Expired &&
                b.EventDate >= fromDate)
            .OrderBy(b => b.EventDate)
            .ThenBy(b => b.TransactionId)
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

    public void Remove(Booking booking)
    {
        _context.Bookings.Remove(booking);
    }

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return await _context.SaveChangesAsync(cancellationToken);
    }
}
