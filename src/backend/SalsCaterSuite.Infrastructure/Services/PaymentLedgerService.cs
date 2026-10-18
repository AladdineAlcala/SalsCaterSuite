using Microsoft.EntityFrameworkCore;
using SalsCaterSuite.Application.Common.Interfaces;
using SalsCaterSuite.Application.Common.Models;
using SalsCaterSuite.Application.Features.Payments.DTOs;
using SalsCaterSuite.Application.Features.Payments.Services;
using SalsCaterSuite.Application.Services;
using SalsCaterSuite.Domain.Entities;

namespace SalsCaterSuite.Infrastructure.Services;

public class PaymentLedgerService : IPaymentLedgerService
{
    private readonly ISalsCaterSuiteDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly BookingAmountDueCalculator _calculator;

    public PaymentLedgerService(
        ISalsCaterSuiteDbContext context,
        ICurrentUserService currentUser,
        BookingAmountDueCalculator calculator)
    {
        _context = context;
        _currentUser = currentUser;
        _calculator = calculator;
    }

    public async Task<Result<PaymentEntryDto>> RecordPaymentAsync(RecordPaymentRequestDto request, CancellationToken cancellationToken = default)
    {
        var booking = await _context.Bookings
            .FirstOrDefaultAsync(b => b.TransactionId == request.TransactionId, cancellationToken);

        if (booking == null)
        {
            return Result<PaymentEntryDto>.Failure("Booking transaction not found.");
        }

        // Generate formatted receipt number e.g. OR-YYYYMMDD-XXXX
        var datePrefix = DateTime.UtcNow.ToString("yyyyMMdd");
        var countToday = await _context.Payments
            .CountAsync(p => p.CreatedDate.HasValue && p.CreatedDate.Value.Date == DateTime.UtcNow.Date, cancellationToken);
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

        _context.Payments.Add(payment);
        await _context.SaveChangesAsync(cancellationToken);

        var dto = new PaymentEntryDto
        {
            PaymentNo = payment.PaymentNo,
            TransactionId = payment.TransactionId.Value,
            DateOfPayment = payment.DateOfPayment,
            Particular = payment.Particular,
            AmountPaid = payment.AmountPaid.Value,
            PaymentMeans = payment.PaymentMeans ?? "Cash",
            CheckNo = payment.CheckNo,
            Notes = payment.Notes,
            CreatedByUser = payment.CreatedByUser
        };

        return Result<PaymentEntryDto>.Success(dto);
    }

    public async Task<Result<LedgerSummaryDto>> GetLedgerAsync(int transactionId, CancellationToken cancellationToken = default)
    {
        var booking = await _context.Bookings
            .Include(b => b.Customer)
            .Include(b => b.Package)
            .Include(b => b.Payments)
            .Include(b => b.Refunds)
            .FirstOrDefaultAsync(b => b.TransactionId == transactionId, cancellationToken);

        if (booking == null)
        {
            return Result<LedgerSummaryDto>.Failure("Booking not found.");
        }

        var payments = booking.Payments
            .OrderBy(p => p.DateOfPayment)
            .Select(p => new PaymentEntryDto
            {
                PaymentNo = p.PaymentNo,
                TransactionId = p.TransactionId ?? transactionId,
                DateOfPayment = p.DateOfPayment,
                Particular = p.Particular,
                PayType = p.PayType,
                AmountPaid = p.AmountPaid ?? 0m,
                PaymentMeans = p.PaymentMeans ?? "Cash",
                CheckNo = p.CheckNo,
                Notes = p.Notes,
                CreatedByUser = p.CreatedByUser
            }).ToList();

        var totalPaid = payments.Sum(p => p.AmountPaid);
        var totalRefunded = booking.Refunds.Sum(r => r.NetAmount ?? 0m);

        var breakdown = _calculator.Calculate(
            booking.PackageAmount,
            booking.NoOfPerson,
            booking.Package?.AmountPax,
            0m,
            booking.ApplyExtendedAmount ?? false,
            0m,
            0m,
            0m,
            null,
            totalPaid
        );

        var summary = new LedgerSummaryDto
        {
            TransactionId = booking.TransactionId,
            CustomerName = booking.Customer?.FullName ?? "Unknown",
            Occasion = booking.Occasion ?? "Event",
            EventDate = booking.StartDate,
            ContractAmount = breakdown.GrossAmount,
            TotalPaid = totalPaid,
            TotalRefunded = totalRefunded,
            BalanceDue = breakdown.RemainingBalance,
            IsFullyPaid = breakdown.IsFullyPaid,
            Payments = payments
        };

        return Result<LedgerSummaryDto>.Success(summary);
    }
}

