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

    /// <summary>
    /// The Bookings &amp; Events Register's <b>base population</b> (REQ-BKG-05, spec §3.3).
    /// <para>
    /// Every non-soft-deleted booking whose status is not <c>Cancelled</c>, narrowed by the
    /// <b>search</b> term and the <b>inclusive</b> event-date range — the two base filters. The
    /// status, tier-type, venue, payment-state and overdue filters are <b>facet</b> filters and
    /// are applied by the Application layer, because the payment state and the overdue
    /// dimension have no column and cannot be expressed in SQL (spec §3.3, RSK-056).
    /// </para>
    /// <para>
    /// The Customer, the Package, the municipality and the Payment rows are loaded, because the
    /// Application layer computes the net amount due, the collected amount, the payment state
    /// and the overdue flag from them. The population is deliberately <b>unpaged</b>: the totals
    /// strip and the facet counts must describe the whole filtered population, not the page.
    /// </para>
    /// <para>
    /// The soft-deleted exclusions are applied by this query — no global query filter is
    /// introduced (DEC-038, DEC-045, DEC-111).
    /// </para>
    /// </summary>
    Task<IReadOnlyList<Booking>> GetRegisterPopulationAsync(
        string? search,
        DateOnly? fromDate,
        DateOnly? toDate,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The live bookings (not soft-deleted, not cancelled) whose event date falls inside the
    /// inclusive range — the near-term commissary load (KPI-38) and the quote's capacity gauge.
    /// <para>
    /// The population is bounded by the date range so the gauge does not read the whole book.
    /// </para>
    /// </summary>
    Task<IReadOnlyList<Booking>> GetLiveBookingsInRangeAsync(
        DateOnly fromDate,
        DateOnly toDate,
        CancellationToken cancellationToken = default);

    Task AddAsync(Booking booking, CancellationToken cancellationToken = default);

    void Update(Booking booking);

    void Remove(Booking booking);

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
