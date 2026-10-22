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

    /// <summary>
    /// The Bookings &amp; Events Register's base population (REQ-BKG-05, spec §3.3).
    /// <para>
    /// Every non-soft-deleted booking whose status is not <c>Cancelled</c>, narrowed by the
    /// search term and the inclusive event-date range. The Customer, the Package, the
    /// municipality and the Payment rows are loaded, because the Application layer derives the
    /// net amount due, the collected amount, the payment state and the overdue flag from them.
    /// </para>
    /// <para>
    /// The population is deliberately <b>unpaged</b>: the totals strip and the facet counts
    /// describe the whole filtered population, not the page (RSK-056).
    /// </para>
    /// <para>
    /// The soft-deleted exclusions are applied by this query — no global query filter is
    /// introduced (DEC-038, DEC-045, DEC-111). A soft-deleted <b>payment</b> is excluded from
    /// the loaded rows, so it is neither summed nor listed anywhere (BR-27).
    /// </para>
    /// </summary>
    public async Task<IReadOnlyList<Booking>> GetRegisterPopulationAsync(
        string? search,
        DateOnly? fromDate,
        DateOnly? toDate,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Bookings
            .AsNoTracking()
            .AsSplitQuery()
            .Include(b => b.Customer)
            .Include(b => b.Package)
            .Include(b => b.ExtendedArea)
            .Include(b => b.Payments.Where(p => !p.IsDeleted))
            .Where(b => !b.IsDeleted && b.Status != BookingStatus.Cancelled);

        if (!string.IsNullOrWhiteSpace(search))
        {
            // The search is a case-insensitive substring over the booking number, the occasion,
            // the venue, the customer's first and last name and the customer's company
            // (BR-32). The customer name is matched on its two parts because the composed
            // FullName is not a stored column and cannot be translated into SQL.
            var term = search.ToLower();
            query = query.Where(b =>
                b.BookingNumber.ToLower().Contains(term) ||
                (b.Occasion != null && b.Occasion.ToLower().Contains(term)) ||
                (b.Venue != null && b.Venue.ToLower().Contains(term)) ||
                b.Customer.FirstName.ToLower().Contains(term) ||
                b.Customer.LastName.ToLower().Contains(term) ||
                (b.Customer.Company != null && b.Customer.Company.ToLower().Contains(term)));
        }

        if (fromDate.HasValue)
        {
            query = query.Where(b => b.EventDate >= fromDate.Value);
        }

        if (toDate.HasValue)
        {
            query = query.Where(b => b.EventDate <= toDate.Value);
        }

        return await query
            .OrderBy(b => b.EventDate)
            .ThenBy(b => b.TransactionId)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// The live bookings (not soft-deleted, not cancelled) whose event date falls inside the
    /// inclusive range — the near-term commissary load (KPI-38).
    /// </summary>
    public async Task<IReadOnlyList<Booking>> GetLiveBookingsInRangeAsync(
        DateOnly fromDate,
        DateOnly toDate,
        CancellationToken cancellationToken = default)
    {
        return await _context.Bookings
            .AsNoTracking()
            .Where(b =>
                !b.IsDeleted &&
                b.Status != BookingStatus.Cancelled &&
                b.EventDate >= fromDate &&
                b.EventDate <= toDate)
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
