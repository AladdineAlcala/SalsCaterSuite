namespace SalsCaterSuite.Application.Common.Interfaces;

using SalsCaterSuite.Domain.Entities;

/// <summary>
/// Booking aggregate persistence (DEC-037). Expressed purely in terms of the
/// <see cref="Booking"/> domain entity — no EF Core types leak through this contract.
/// <para>
/// Atomicity: every repository implementation shares the request-scoped
/// <c>SalsCaterSuiteDbContext</c>, so a single <see cref="SaveChangesAsync"/> commits
/// the entire pending change set, including changes staged through other repositories
/// in the same scope. The booking is the aggregate root, so its repository owns the
/// commit boundary — including refund rows staged via <see cref="IRefundRepository"/>.
/// </para>
/// </summary>
public interface IBookingRepository
{
    /// <summary>Loads a booking by <c>TransactionId</c> without navigation collections.</summary>
    Task<Booking?> GetByIdAsync(int transactionId, CancellationToken cancellationToken = default);

    /// <summary>Loads a booking with Customer, Package, Payments and Refunds.</summary>
    Task<Booking?> GetWithDetailsAsync(int transactionId, CancellationToken cancellationToken = default);

    /// <summary>Loads a booking with its Payments (used by the cancellation path).</summary>
    Task<Booking?> GetWithPaymentsAsync(int transactionId, CancellationToken cancellationToken = default);

    /// <summary>Loads bookings whose event date falls within the inclusive range.</summary>
    Task<IReadOnlyList<Booking>> GetByDateRangeAsync(DateOnly fromDate, DateOnly toDate, CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads non-cancelled bookings in the inclusive event-date range with Customer,
    /// Package and Payments, ordered by event date — the calendar feed projection.
    /// </summary>
    Task<IReadOnlyList<Booking>> GetForCalendarAsync(DateOnly fromDate, DateOnly toDate, CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads the dashboard's period population: bookings whose event date falls in the
    /// inclusive range, excluding cancelled, expired and soft-deleted rows, with the Customer
    /// and the Payments loaded (REQ-DSH-01, spec §7 R2).
    /// <para>
    /// The soft-delete exclusion is scoped to this dashboard query — no global query filter is
    /// applied (DEC-038, spec §6.13).
    /// </para>
    /// </summary>
    Task<IReadOnlyList<Booking>> GetDashboardPeriodBookingsAsync(DateOnly fromDate, DateOnly toDate, CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads the dashboard's receivables population: every non-cancelled, non-soft-deleted
    /// booking with its Customer and Payments (REQ-DSH-01, spec §7 R3).
    /// </summary>
    Task<IReadOnlyList<Booking>> GetDashboardReceivablesBookingsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads the dashboard's upcoming schedule: bookings whose event date is on or after
    /// <paramref name="fromDate"/>, excluding cancelled, expired and soft-deleted rows, with
    /// the Customer and the Payments loaded, ordered by event date then transaction id
    /// (REQ-DSH-01, spec §7 R5). The limit is applied by the Application layer, not in SQL.
    /// </summary>
    Task<IReadOnlyList<Booking>> GetDashboardUpcomingBookingsAsync(DateOnly fromDate, CancellationToken cancellationToken = default);

    Task AddAsync(Booking booking, CancellationToken cancellationToken = default);

    void Update(Booking booking);

    void Remove(Booking booking);

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
