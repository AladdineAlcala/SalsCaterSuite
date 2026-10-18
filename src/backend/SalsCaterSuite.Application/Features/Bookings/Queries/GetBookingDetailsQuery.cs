using Microsoft.EntityFrameworkCore;
using SalsCaterSuite.Application.Common.Interfaces;
using SalsCaterSuite.Application.Common.Models;
using SalsCaterSuite.Application.Features.Bookings.DTOs;
using SalsCaterSuite.Application.Services;

namespace SalsCaterSuite.Application.Features.Bookings.Queries;

public class GetBookingDetailsQuery
{
    private readonly ISalsCaterSuiteDbContext _context;
    private readonly BookingAmountDueCalculator _calculator;

    public GetBookingDetailsQuery(ISalsCaterSuiteDbContext context, BookingAmountDueCalculator calculator)
    {
        _context = context;
        _calculator = calculator;
    }

    public async Task<Result<BookingDto>> ExecuteAsync(int transactionId, CancellationToken cancellationToken = default)
    {
        var booking = await _context.Bookings
            .Include(b => b.Customer)
            .Include(b => b.Package)
            .Include(b => b.Payments)
            .FirstOrDefaultAsync(b => b.TransactionId == transactionId, cancellationToken);

        if (booking == null)
        {
            return Result<BookingDto>.Failure("Booking not found.");
        }

        // Recompute from the frozen price snapshot using the calculator.
        // Uses the booking's own stored snapshot values — not the live Package price.
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
            totalPaymentsReceived: booking.AmountPaid
        );

        var dto = new BookingDto
        {
            TransactionId = booking.TransactionId,
            BookingNumber = booking.BookingNumber,
            TransactionDate = booking.TransactionDate,
            BookType = booking.BookType.ToString(),
            TypeOfService = booking.TypeOfService.ToString(),
            CustomerId = booking.CustomerId,
            CustomerName = booking.Customer?.FullName ?? string.Empty,
            CustomerContact = booking.Customer?.Contact1 ?? booking.Customer?.Contact2,
            NumberOfHeads = booking.NumberOfHeads,
            Occasion = booking.Occasion,
            Venue = booking.Venue,
            EventDate = booking.EventDate,
            EventStartTime = booking.EventStartTime,
            EventEndTime = booking.EventEndTime,
            EventColor = booking.EventColor,
            PackageId = booking.PackageId,
            PackageName = booking.PackageName,
            Reference = booking.Reference,

            // Price snapshot (frozen)
            PricePerHead = booking.PricePerHead,
            PackageAmount = booking.PackageAmount,

            // Financial breakdown
            Subtotal = breakdown.GrossAmount,
            DiscountType = booking.DiscountType.ToString(),
            DiscountPercentage = booking.DiscountPercentage,
            DiscountAmount = breakdown.DiscountAmount,
            TaxAmount = booking.TaxAmount,
            AmountDue = breakdown.NetAmountDue,
            DepositAmount = booking.DepositAmount,
            AmountPaid = booking.AmountPaid,
            Balance = breakdown.RemainingBalance,

            // Status
            Status = booking.Status.ToString(),
            ReminderSent = booking.ReminderSent,
        };

        return Result<BookingDto>.Success(dto);
    }
}
