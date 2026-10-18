using Microsoft.EntityFrameworkCore;
using SalsCaterSuite.Application.Common.Interfaces;
using SalsCaterSuite.Application.Common.Models;
using SalsCaterSuite.Domain.Entities;
using SalsCaterSuite.Domain.Enums;

namespace SalsCaterSuite.Application.Features.Bookings.Commands;

public class CancelBookingCommand
{
    private readonly ISalsCaterSuiteDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public CancelBookingCommand(ISalsCaterSuiteDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Result> ExecuteAsync(string transactionId, string reason, decimal? refundAmount, CancellationToken cancellationToken = default)
    {
        var booking = await _context.Bookings
            .Include(b => b.Payments)
            .FirstOrDefaultAsync(b => b.BookingNumber == transactionId, cancellationToken);

        if (booking == null)
        {
            return Result.Failure("Booking transaction not found.");
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
            _context.Refunds.Add(refund);
        }

        await _context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
