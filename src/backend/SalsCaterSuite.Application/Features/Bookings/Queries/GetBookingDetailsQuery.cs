using SalsCaterSuite.Application.Common.Interfaces;
using SalsCaterSuite.Application.Common.Models;
using SalsCaterSuite.Application.Features.Bookings.DTOs.Request;
using SalsCaterSuite.Application.Features.Bookings.DTOs.Response;
using SalsCaterSuite.Application.Features.Bookings.Models;
using SalsCaterSuite.Application.Features.Bookings.Services;
using SalsCaterSuite.Application.Services;
using SalsCaterSuite.Domain.Entities;
using SalsCaterSuite.Domain.Enums;

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

    public async Task<Result<BookingResponse>> ExecuteAsync(int transactionId, CancellationToken cancellationToken = default)
    {
        var booking = await _bookings.GetWithDetailsAsync(transactionId, cancellationToken);

        if (booking == null)
        {
            return Result<BookingResponse>.Failure("Booking not found.");
        }

        // The sum of Payment rows is the authoritative "amount paid" (DEC-012).
        // Booking.AmountPaid is only a denormalized cache; use the ledger figure here
        // so the details view always reconciles with GET .../ledger.
        // A soft-deleted payment is excluded (BR-27, DEC-111) — it is not money the business
        // holds, and it is not a receipt the customer may see.
        var totalPayments = BookingPaymentPopulation.Collected(booking);

        // Recompute from the frozen price snapshot using the calculator.
        // Uses the booking's own stored snapshot values — not the live Package price.
        // The shared overload keeps the argument set in one place (DEC-042).
        var breakdown = _calculator.Calculate(booking, totalPayments);

        var dto = new BookingResponse
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
            EventEndDate = booking.EventEndDate,
            EventStartTime = booking.EventStartTime,
            EventEndTime = booking.EventEndTime,
            CallTime = booking.CallTime,
            PackoutTime = booking.PackoutTime,
            EventColor = booking.EventColor,
            PackageId = booking.PackageId,
            PackageName = booking.PackageName,
            Reference = booking.Reference,

            // Price snapshot (frozen)
            PricePerHead = booking.PricePerHead,
            PackageAmount = booking.PackageAmount,

            // The frozen out-of-town surcharge (BR-26, DEC-116) — the amount the booking was
            // created with, never the municipality's current rate.
            ExtendedAreaAmount = booking.ExtendedAreaAmount,

            // The per-guest rate the frozen surcharge was derived from, so a reader of the
            // printed contract can see how the amount was derived (rate × pax, BR-17). It is
            // null when no surcharge was applied — "not stated" is null, never zero (BR-20).
            ExtendedAreaRatePerGuest = ResolveExtendedAreaRatePerGuest(booking),

            // Financial breakdown
            Subtotal = breakdown.GrossAmount,
            DiscountType = booking.DiscountType.ToString(),
            DiscountPercentage = booking.DiscountPercentage,
            DiscountCode = booking.DiscountCode,
            DiscountAmount = breakdown.DiscountAmount,
            TaxAmount = booking.TaxAmount,
            AddonsTotal = breakdown.AddonsAmount,
            AmountDue = breakdown.NetAmountDue,
            DepositAmount = booking.DepositAmount,
            AmountPaid = totalPayments,
            Balance = breakdown.RemainingBalance,

            // Lifecycle and payment progress are separate (DEC-177). The payment state is derived
            // from the authoritative payment sum, not from the stored column, so a stale cache
            // cannot be published.
            Status = booking.Status,
            PaymentState = PaymentStateRules.Classify(totalPayments, breakdown.NetAmountDue),
            ReminderSent = booking.ReminderSent,
        };

        return Result<BookingResponse>.Success(dto);
    }

    /// <summary>
    /// The per-guest rate the frozen surcharge was derived from — the frozen amount divided by
    /// the headcount, which recovers the municipality's rate exactly, because a two-decimal rate
    /// times an integer headcount is exact at two decimals (BR-17, BR-26).
    /// <para>
    /// The division is <b>verified</b> before the rate is published: if the recovered rate does
    /// <b>not</b> reconstruct the frozen amount exactly at two decimals, the rate is
    /// <c>null</c>. A rounded rate that does not tie out would put an arithmetic error on a
    /// <b>signed</b> document, and the document then prints the amount and the headcount
    /// <b>without</b> a rate. "Not stated" is null, never zero (BR-20).
    /// </para>
    /// </summary>
    private static decimal? ResolveExtendedAreaRatePerGuest(Booking booking)
    {
        if (booking.ExtendedAreaAmount <= 0m || booking.NumberOfHeads <= 0)
        {
            return null;
        }

        var rate = BookingAmountDueCalculator.Round2(booking.ExtendedAreaAmount / booking.NumberOfHeads);

        return BookingAmountDueCalculator.Round2(rate * booking.NumberOfHeads) == booking.ExtendedAreaAmount
            ? rate
            : null;
    }
}
