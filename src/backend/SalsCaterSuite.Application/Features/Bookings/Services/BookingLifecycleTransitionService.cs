using SalsCaterSuite.Application.Common.Interfaces;
using SalsCaterSuite.Domain.Entities;

namespace SalsCaterSuite.Application.Features.Bookings.Services;

public readonly record struct BookingLifecycleBatchResult(int Committed, int Selected, bool BatchWasFull);

/// <summary>
/// Applies the approved BG-04 transitions. It never changes payment state or amounts.
/// </summary>
public interface IBookingLifecycleTransitionService
{
    Task<BookingLifecycleBatchResult> ProcessDueBatchAsync(
        int batchSize,
        CancellationToken cancellationToken = default);
}

public sealed class BookingLifecycleTransitionService : IBookingLifecycleTransitionService
{
    private readonly IBookingLifecycleRepository _repository;
    private readonly IDateTime _clock;

    public BookingLifecycleTransitionService(IBookingLifecycleRepository repository, IDateTime clock)
    {
        _repository = repository;
        _clock = clock;
    }

    public async Task<BookingLifecycleBatchResult> ProcessDueBatchAsync(
        int batchSize,
        CancellationToken cancellationToken = default)
    {
        if (batchSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(batchSize));
        }

        var now = DateTime.SpecifyKind(_clock.UtcNow, DateTimeKind.Utc);
        var ids = await _repository.GetDueIdsAsync(now, batchSize, cancellationToken);
        var committed = 0;

        foreach (var id in ids)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (await TryTransitionAsync(id, now, cancellationToken))
            {
                committed++;
            }
        }

        return new BookingLifecycleBatchResult(committed, ids.Count, ids.Count == batchSize);
    }

    private async Task<bool> TryTransitionAsync(int transactionId, DateTime utcNow, CancellationToken cancellationToken)
    {
        var booking = await _repository.GetTrackedAsync(transactionId, cancellationToken);
        if (booking is null)
        {
            return false;
        }

        var due = BookingLifecycleSchedule.DueTransitions(
            booking.Status,
            booking.IsDeleted,
            booking.EventDate,
            booking.EventStartTime,
            booking.EventEndDate,
            booking.PackoutTime,
            booking.EventEndTime,
            utcNow);

        if (due.Count == 0)
        {
            return false;
        }

        var applied = 0;
        foreach (var transition in due)
        {
            if (!booking.TryApplyScheduledTransition(transition.From, transition.To))
            {
                break;
            }

            _repository.StageHistory(BookingStatusHistory.ForTransition(
                booking.TransactionId,
                transition.From,
                transition.To,
                transition.EffectiveAtUtc,
                utcNow));
            applied++;
        }

        if (applied == 0)
        {
            return false;
        }

        return await _repository.TryCommitAsync(cancellationToken);
    }
}
