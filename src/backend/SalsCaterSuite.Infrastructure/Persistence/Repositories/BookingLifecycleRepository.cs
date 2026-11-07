using Microsoft.EntityFrameworkCore;
using SalsCaterSuite.Application.Common.Interfaces;
using SalsCaterSuite.Application.Features.Bookings.Services;
using SalsCaterSuite.Domain.Entities;
using SalsCaterSuite.Domain.Enums;

namespace SalsCaterSuite.Infrastructure.Persistence.Repositories;

/// <summary>
/// Due-only BG-04 query and the atomic status/history commit (DEC-178, DEC-037).
/// Asia/Manila is applied as the offset of <paramref name="utcNow"/> so the SQL comparison
/// is the same instant the schedule uses.
/// </summary>
public class BookingLifecycleRepository : IBookingLifecycleRepository
{
    private readonly SalsCaterSuiteDbContext _context;

    public BookingLifecycleRepository(SalsCaterSuiteDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<int>> GetDueIdsAsync(
        DateTime utcNow,
        int batchSize,
        CancellationToken cancellationToken = default)
    {
        var utc = DateTime.SpecifyKind(utcNow, DateTimeKind.Utc);
        var offsetMinutes = (int)BookingLifecycleSchedule.Manila
            .GetUtcOffset(new DateTimeOffset(utc, TimeSpan.Zero))
            .TotalMinutes;
        var confirmed = (int)BookingStatus.Confirmed;
        var inProgress = (int)BookingStatus.InProgress;

        // Local Manila wall time converted to UTC is local minus the zone offset.
        // DATEADD(hour, 1, ...) rolls a late pack-out into the next calendar day.
        var ids = await _context.Database.SqlQuery<int>($"""
            SELECT TOP ({batchSize}) b.[trn_Id] AS [Value]
            FROM [Booking] AS b
            WHERE b.[is_deleted] = 0
              AND (
                    (b.[booking_status] = {confirmed}
                     AND DATEADD(minute, -{offsetMinutes},
                            DATEADD(second, DATEDIFF(second, CAST('00:00:00' AS time), b.[startTime]), CAST(b.[eventDate] AS datetime2)))
                         <= {utc})
                 OR (b.[booking_status] = {inProgress}
                     AND DATEADD(minute, -{offsetMinutes},
                            DATEADD(hour, 1,
                                DATEADD(second, DATEDIFF(second, CAST('00:00:00' AS time), ISNULL(b.[packoutTime], b.[endTime])), CAST(b.[eventEndDate] AS datetime2))))
                         <= {utc})
                  )
            ORDER BY b.[eventDate], b.[trn_Id]
            """)
            .ToListAsync(cancellationToken);

        return ids;
    }

    public Task<Booking?> GetTrackedAsync(int transactionId, CancellationToken cancellationToken = default) =>
        _context.Bookings.FirstOrDefaultAsync(b => b.TransactionId == transactionId, cancellationToken);

    public void StageHistory(BookingStatusHistory history) =>
        _context.BookingStatusHistories.Add(history);

    public async Task<bool> TryCommitAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await _context.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateConcurrencyException ex)
        {
            foreach (var entry in ex.Entries)
            {
                entry.State = EntityState.Detached;
            }

            foreach (var entry in _context.ChangeTracker.Entries<BookingStatusHistory>()
                         .Where(entry => entry.State == EntityState.Added)
                         .ToList())
            {
                entry.State = EntityState.Detached;
            }

            return false;
        }
    }
}
