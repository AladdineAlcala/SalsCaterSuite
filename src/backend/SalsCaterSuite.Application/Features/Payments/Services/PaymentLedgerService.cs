using SalsCaterSuite.Application.Common.Interfaces;
using SalsCaterSuite.Application.Common.Models;
using SalsCaterSuite.Application.Features.Payments.DTOs;
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

    public async Task<Result<PaymentEntryDto>> RecordPaymentAsync(RecordPaymentRequestDto request, CancellationToken cancellationToken = default)
    {
        var booking = await _bookings.GetByIdAsync(request.TransactionId, cancellationToken);

        if (booking == null)
        {
            return Result<PaymentEntryDto>.Failure("Booking transaction not found.");
        }

        // Generate formatted receipt number e.g. OR-YYYYMMDD-XXXX
        var datePrefix = DateTime.UtcNow.ToString("yyyyMMdd");
        var countToday = await _payments.CountCreatedOnDateAsync(DateTime.UtcNow, cancellationToken);
        var paymentNo = $"OR-{datePrefix}-{(countToday + 1):D4}";

        var payment = new Payment
        {
            PaymentNo = paymentNo,
            TransactionId = request.TransactionId,
            DateOfPayment = DateTime.UtcNow,
            Particular = request.Particular,
            AmountPaid = request.AmountPaid,
            PaymentMeans = request.PaymentMeans,
            CheckNo = request.CheckNo,
            Notes = request.Notes,
            CreatedByUser = _currentUser.UserName ?? "cashier",
            CreatedDate = DateTime.UtcNow
        };

        await _payments.AddAsync(payment, cancellationToken);

        // Refresh the denormalized AmountPaid cache / booking status through the
        // domain method. Domain rule violations must surface as a failed result
        // (HTTP 400), never an unhandled 500 (DEC-012, DEC-014).
        try
        {
            booking.RecordPayment(request.AmountPaid);
        }
        catch (DomainException ex)
        {
            // Nothing has been persisted yet; drop the staged payment so it is not
            // left orphaned in the change set.
            _payments.Remove(payment);
            return Result<PaymentEntryDto>.Failure(ex.Message);
        }

        // Single commit for the payment row and the booking's refreshed status cache.
        await _payments.SaveChangesAsync(cancellationToken);

        var dto = new PaymentEntryDto
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

        return Result<PaymentEntryDto>.Success(dto);
    }

    public async Task<Result<LedgerSummaryDto>> GetLedgerAsync(int transactionId, CancellationToken cancellationToken = default)
    {
        var booking = await _bookings.GetWithDetailsAsync(transactionId, cancellationToken);

        if (booking == null)
        {
            return Result<LedgerSummaryDto>.Failure("Booking not found.");
        }

        var payments = booking.Payments
            .OrderBy(p => p.DateOfPayment)
            .Select(p => new PaymentEntryDto
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
