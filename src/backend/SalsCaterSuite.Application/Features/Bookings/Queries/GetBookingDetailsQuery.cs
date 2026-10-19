using SalsCaterSuite.Application.Common.Interfaces;
using SalsCaterSuite.Application.Common.Models;
using SalsCaterSuite.Application.Features.Bookings.DTOs;
using SalsCaterSuite.Application.Services;

namespace SalsCaterSuite.Application.Features.Bookings.Queries;

public class GetBookingDetailsQuery
{
    private readonly IBookingRepository _bookings;
    private readonly BookingAmountDueCalculator _calculator;

    public GetBookingDetailsQuery(IBookingRepository bookings, BookingAmountDueCalculator calculator)
    {
        _bookings = bookings;
        _calculator = calculator;
    }

    public async Task<Result<BookingDto>> ExecuteAsync(int transactionId, CancellationToken cancellationToken = default)
    {
        var booking = await _bookings.GetWithDetailsAsync(transactionId, cancellationToken);

        if (booking == null)
        {
            return Result<BookingDto>.Failure("Booking not found.");
        }

        // The sum of Payment rows is the authoritative "amount paid" (DEC-012).
        // Booking.AmountPaid is only a denormalized cache; use the ledger figure here
        // so the details view always reconciles with GET .../ledger.
        var totalPayments = booking.Payments.Sum(p => p.AmountPaid);

        // Recompute from the frozen price snapshot using the calculator.
        // Uses the booking's own stored snapshot values — not the live Package price.
        // The shared overload keeps the argument set in one place (DEC-042).
        var breakdown = _calculator.Calculate(booking, totalPayments);

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
            AmountPaid = totalPayments,
            Balance = breakdown.RemainingBalance,

            // Status
            Status = booking.Status,
            ReminderSent = booking.ReminderSent,
        };

        return Result<BookingDto>.Success(dto);
    }
}
