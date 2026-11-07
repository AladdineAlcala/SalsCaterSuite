using SalsCaterSuite.Domain.Entities;

namespace SalsCaterSuite.Application.Common.Interfaces;

/// <summary>
/// Persistence for BG-04 (DEC-178, DEC-037). The due query and the commit live here so the
/// Application service never references EF Core. History and status share one SaveChanges.
/// </summary>
public interface IBookingLifecycleRepository
{
    /// <summary>
    /// Identifiers of non-deleted Confirmed or InProgress bookings whose approved threshold
    /// has passed, ordered by event date then transaction id, limited to <paramref name="batchSize"/>.
    /// </summary>
    Task<IReadOnlyList<int>> GetDueIdsAsync(
        DateTime utcNow,
        int batchSize,
        CancellationToken cancellationToken = default);

    Task<Booking?> GetTrackedAsync(int transactionId, CancellationToken cancellationToken = default);

    void StageHistory(BookingStatusHistory history);

    /// <summary>
    /// Commits the tracked status change and staged history. Returns false when the booking's
    /// row version lost a race; the failed entries are detached and nothing from that attempt remains.
    /// </summary>
    Task<bool> TryCommitAsync(CancellationToken cancellationToken = default);
}
