using SalsCaterSuite.Application.Features.Payments.DTOs;
using SalsCaterSuite.Application.Common.Interfaces;
using SalsCaterSuite.Application.Common.Models;
using SalsCaterSuite.Application.Features.Bookings.Services;
using SalsCaterSuite.Application.Features.Payments.DTOs.Request;
using SalsCaterSuite.Application.Features.Payments.DTOs.Response;
using SalsCaterSuite.Application.Services;
using SalsCaterSuite.Domain.Entities;
using SalsCaterSuite.Domain.Exceptions;

namespace SalsCaterSuite.Application.Features.Payments.Services;

/// <summary>
/// The single authoritative payment/ledger implementation (DEC-012, DEC-015).
/// Both <c>/api/payments/ledger/{id}</c> and <c>/api/bookings/{id}/ledger</c>
/// flow through this service so there is exactly one source of ledger truth.
/// </summary>
public class PaymentLedgerService : IPaymentLedgerService
{
    private readonly IBookingRepository _bookings;
    private readonly IPaymentRepository _payments;
    private readonly ICurrentUserService _currentUser;
    private readonly BookingAmountDueCalculator _calculator;

    public PaymentLedgerService(
        IBookingRepository bookings,
        IPaymentRepository payments,
        ICurrentUserService currentUser,
        BookingAmountDueCalculator calculator)
    {
        _bookings = bookings;
        _payments = payments;
        _currentUser = currentUser;
        _calculator = calculator;
    }

    public async Task<Result<PaymentEntryResponse>> RecordPaymentAsync(RecordPaymentRequest request, CancellationToken cancellationToken = default)
    {
        // The booking is loaded with its payment rows, because the overpayment guard needs the
        // authoritative collected amount (DEC-118).
        var booking = await _bookings.GetWithPaymentsAsync(request.TransactionId, cancellationToken);

        if (booking == null)
        {
            return Result<PaymentEntryResponse>.Failure("Booking transaction not found.");
        }

        // BR-05's overpayment prohibition (DEC-118). A payment that would take the total above
        // the net amount due is refused, and NOTHING is written: no payment row, no status
        // change and no change to the cached AmountPaid.
        var collected = BookingPaymentPopulation.Collected(booking);
        var netAmountDue = _calculator.Calculate(booking, collected).NetAmountDue;

        if (collected + request.AmountPaid > netAmountDue)
        {
            return Result<PaymentEntryResponse>.Failure(
                "The payment would take the total collected above the net amount due of the booking.");
        }

        // The receipt number comes from the ONE generator, shared with the booking create path
        // (DEC-112). The race the sequence can still lose stays open (RSK-013).
        var now = DateTime.UtcNow;
        var paymentNo = await ReceiptNumberGenerator.GenerateAsync(_payments, now, cancellationToken);

        var payment = new Payment
        {
            PaymentNo = paymentNo,
            TransactionId = request.TransactionId,
            DateOfPayment = now,
            Particular = request.Particular,
            AmountPaid = request.AmountPaid,
            PaymentMeans = request.PaymentMeans,
            CheckNo = request.CheckNo,
            Notes = request.Notes,
            CreatedByUser = _currentUser.UserName ?? "cashier",
            CreatedDate = DateTime.UtcNow
        };

        await _payments.AddAsync(payment, cancellationToken);

        // Refresh the denormalized AmountPaid cache. Payment progress is overwritten from the
        // authoritative non-soft-deleted sum plus this payment, and from the calculator's net
        // amount due. Lifecycle status is not an input and is not written (DEC-177, DEC-012).
        try
        {
            booking.RecordPayment(request.AmountPaid);
            booking.RefreshPaymentStatus(collected + request.AmountPaid, netAmountDue);
        }
        catch (DomainException ex)
        {
            // Nothing has been persisted yet; drop the staged payment so it is not
            // left orphaned in the change set.
            _payments.Remove(payment);
            return Result<PaymentEntryResponse>.Failure(ex.Message);
        }

        // Single commit for the payment row and the booking's refreshed payment-state cache.
        await _payments.SaveChangesAsync(cancellationToken);

        var dto = new PaymentEntryResponse
        {
            PaymentNo = payment.PaymentNo,
            TransactionId = payment.TransactionId,
            DateOfPayment = payment.DateOfPayment,
            Particular = payment.Particular,
            AmountPaid = payment.AmountPaid,
            PaymentMeans = payment.PaymentMeans,
            CheckNo = payment.CheckNo,
            Notes = payment.Notes,
            CreatedByUser = payment.CreatedByUser
        };

        return Result<PaymentEntryResponse>.Success(dto);
    }

    public async Task<Result<LedgerSummaryDto>> GetLedgerAsync(int transactionId, CancellationToken cancellationToken = default)
    {
        var booking = await _bookings.GetWithDetailsAsync(transactionId, cancellationToken);

        if (booking == null)
        {
            return Result<LedgerSummaryDto>.Failure("Booking not found.");
        }

        var payments = booking.Payments
            .Where(p => !p.IsDeleted)          // a soft-deleted payment is not listed (DEC-111)
            .OrderBy(p => p.DateOfPayment)
            .Select(p => new PaymentEntryResponse
            {
                PaymentNo = p.PaymentNo,
                TransactionId = p.TransactionId,
                DateOfPayment = p.DateOfPayment,
                Particular = p.Particular,
                AmountPaid = p.AmountPaid,
                PaymentMeans = p.PaymentMeans,
                CheckNo = p.CheckNo,
                Notes = p.Notes,
                CreatedByUser = p.CreatedByUser
            }).ToList();

        // The sum of Payment rows is authoritative (DEC-012).
        var totalPaid = payments.Sum(p => p.AmountPaid);
        // Refunds are reported separately and never reduce BalanceDue (DEC-013).
        var totalRefunded = booking.Refunds.Sum(r => r.NetAmount);

        // The net amount due and the balance come from the single shared calculator overload,
        // so the ledger and the operations dashboard can never disagree (DEC-042, AC-B3).
        // The arguments are identical to the ones this service has always used.
        var breakdown = _calculator.Calculate(booking, totalPaid);

        var summary = new LedgerSummaryDto
        {
            TransactionId = booking.TransactionId,
            CustomerName = booking.Customer?.FullName ?? "Unknown",
            Occasion = booking.Occasion ?? "Event",
            EventDate = booking.EventDate,
            ContractAmount = breakdown.NetAmountDue,
            TotalPaid = totalPaid,
            TotalRefunded = totalRefunded,
            BalanceDue = breakdown.RemainingBalance,
            IsFullyPaid = breakdown.IsFullyPaid,
            Payments = payments
        };

        return Result<LedgerSummaryDto>.Success(summary);
    }
}
