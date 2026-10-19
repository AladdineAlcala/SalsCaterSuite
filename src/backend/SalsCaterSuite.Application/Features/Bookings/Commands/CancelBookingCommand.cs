using SalsCaterSuite.Application.Common.Interfaces;
using SalsCaterSuite.Application.Common.Models;
using SalsCaterSuite.Domain.Entities;
using SalsCaterSuite.Domain.Enums;

namespace SalsCaterSuite.Application.Features.Bookings.Commands;

/// <summary>
/// Cancels a booking and, when a refund amount is supplied, records the refund.
/// <para>
/// The booking is identified by its <c>TransactionId</c> — the same identifier
/// used by every other bookings endpoint — so the Web API layer never needs to
/// touch persistence to resolve the booking.
/// </para>
/// </summary>
public class CancelBookingCommand
{
    private readonly IBookingRepository _bookings;
    private readonly IRefundRepository _refunds;

    public CancelBookingCommand(IBookingRepository bookings, IRefundRepository refunds)
    {
        _bookings = bookings;
        _refunds = refunds;
    }

    public async Task<Result> ExecuteAsync(
        int transactionId,
        string reason,
        decimal? refundAmount,
        CancellationToken cancellationToken = default)
    {
        var booking = await _bookings.GetWithPaymentsAsync(transactionId, cancellationToken);

        if (booking == null)
        {
            return Result.Failure("Booking transaction not found.");
        }

        // Authoritative payments sum (DEC-012): the sum of the Payment rows, never the
        // denormalized Booking.AmountPaid cache.
        var totalPaymentsReceived = booking.Payments.Sum(p => p.AmountPaid);

        // The refund request is validated BEFORE any mutation so that a rejected refund
        // leaves the booking uncancelled and creates no Refund row (DEC-024 / F7).
        if (refundAmount.HasValue && refundAmount.Value < 0)
        {
            return Result.Failure("Refund amount must not be negative.");
        }

        if (refundAmount.HasValue && refundAmount.Value > totalPaymentsReceived)
        {
            return Result.Failure("Refund amount cannot exceed the total payments received.");
        }

        // Domain method enforces all valid-state rules (already cancelled, already completed, etc.)
        try
        {
            booking.Cancel();
        }
        catch (Domain.Exceptions.DomainException ex)
        {
            return Result.Failure(ex.Message);
        }

        booking.UpdatedDate = DateTime.UtcNow;

        if (refundAmount.HasValue && refundAmount.Value > 0)
        {
            var refund = new Refund
            {
                TransactionId = booking.TransactionId,
                RefundDate = DateTime.UtcNow,
                RefundReason = reason,
                RefundAmount = refundAmount.Value,
                NetAmount = refundAmount.Value,
                Status = RefundStatus.Processed
            };
            await _refunds.AddAsync(refund, cancellationToken);
        }

        // Single commit: the status transition and the optional refund insert are
        // persisted together (both repositories share the request-scoped DbContext).
        await _bookings.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
