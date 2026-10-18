using Microsoft.EntityFrameworkCore;
using SalsCaterSuite.Application.Common.Interfaces;
using SalsCaterSuite.Application.Common.Models;
using SalsCaterSuite.Application.Features.Payments.DTOs;
using SalsCaterSuite.Application.Services;

namespace SalsCaterSuite.Application.Features.Bookings.Queries;

public class GetBookingLedgerQuery
{
    private readonly ISalsCaterSuiteDbContext _context;
    private readonly BookingAmountDueCalculator _calculator;

    public GetBookingLedgerQuery(ISalsCaterSuiteDbContext context, BookingAmountDueCalculator calculator)
    {
        _context = context;
        _calculator = calculator;
    }

    public async Task<Result<LedgerSummaryDto>> ExecuteAsync(int transactionId, CancellationToken cancellationToken = default)
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
                TransactionId = p.TransactionId,
                DateOfPayment = p.DateOfPayment,
                Particular = p.Particular,
                AmountPaid = p.AmountPaid,
                PaymentMeans = p.PaymentMeans.ToString(),
                CheckNo = p.CheckNo,
                Notes = p.Notes,
                CreatedByUser = p.CreatedByUser
            }).ToList();

        var totalPaid = payments.Sum(p => p.AmountPaid);
        var totalRefunded = booking.Refunds.Sum(r => r.NetAmount);

        // Use the frozen price snapshot — never the live package price.
        var breakdown = _calculator.Calculate(
            packageAmount: booking.PackageAmount,
            numberOfHeads: booking.NumberOfHeads,
            pricePerPax: booking.PricePerHead,
            extendedAreaRate: 0m,
            applyExtendedArea: booking.ApplyExtendedAmount,
            addonsTotal: 0m,
            otherChargesTotal: 0m,
            discountPercentage: booking.DiscountPercentage,
            discountFixedAmount: booking.DiscountAmount > 0 ? booking.DiscountAmount : null,
            totalPaymentsReceived: totalPaid
        );

        var ledger = new LedgerSummaryDto
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

        return Result<LedgerSummaryDto>.Success(ledger);
    }
}
