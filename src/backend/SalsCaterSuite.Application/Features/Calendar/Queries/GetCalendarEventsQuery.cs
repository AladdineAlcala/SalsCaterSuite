using SalsCaterSuite.Application.Common.Interfaces;
using SalsCaterSuite.Application.Common.Models;
using SalsCaterSuite.Application.Features.Bookings.Services;
using SalsCaterSuite.Application.Features.Calendar.DTOs.Response;
using SalsCaterSuite.Application.Services;
using SalsCaterSuite.Domain.Enums;

namespace SalsCaterSuite.Application.Features.Calendar.Queries;

/// <summary>GET /api/calendar/events — calendar feed with server-computed balances.</summary>
public class GetCalendarEventsQuery
{
    private readonly IBookingRepository _bookings;
    private readonly BookingAmountDueCalculator _calculator;

    public GetCalendarEventsQuery(IBookingRepository bookings, BookingAmountDueCalculator calculator)
    {
        _bookings = bookings;
        _calculator = calculator;
    }

    public async Task<Result<List<CalendarEventResponse>>> ExecuteAsync(
        DateTime? start,
        DateTime? end,
        CancellationToken cancellationToken = default)
    {
        var from = start ?? DateTime.UtcNow.AddMonths(-1);
        var to = end ?? DateTime.UtcNow.AddMonths(3);
        var fromDate = DateOnly.FromDateTime(from);
        var toDate = DateOnly.FromDateTime(to);

        var bookings = await _bookings.GetForCalendarAsync(fromDate, toDate, cancellationToken);

        var events = bookings.Select(b =>
        {
            // The sum of Payment rows is the authoritative "amount paid" (DEC-012).
            // A soft-deleted payment is excluded (BR-27, DEC-111), exactly as the operations
            // dashboard excludes it.
            var totalPaid = BookingPaymentPopulation.Collected(b);

            // Use the frozen price snapshot — never the live package price.
            // The shared overload keeps the argument set in one place (DEC-042).
            var breakdown = _calculator.Calculate(b, totalPaid);

            var status = b.Status is BookingStatus.Completed or BookingStatus.InProgress ? "Served"
                       : breakdown.IsFullyPaid ? "Fully Paid"
                       : breakdown.RemainingBalance > 0 ? "With Balance"
                       : "Confirmed";

            var eventStart = b.EventDate.ToDateTime(TimeOnly.FromTimeSpan(b.EventStartTime));
            var eventEnd = b.EventEndDate.ToDateTime(TimeOnly.FromTimeSpan(b.EventEndTime));

            return new CalendarEventResponse
            {
                Id = b.TransactionId.ToString(),
                Title = $"{b.Customer?.FullName ?? "Unknown"} - {b.Occasion}",
                Start = eventStart,
                End = eventEnd,
                BackgroundColor = b.EventColor ?? "#2563eb",
                BorderColor = b.EventColor ?? "#1d4ed8",
                TextColor = "#ffffff",
                AllDay = false,
                ExtendedProps = new ExtendedEventPropsResponse
                {
                    TransactionId = b.TransactionId,
                    CustomerName = b.Customer?.FullName ?? "Unknown",
                    Venue = b.Venue ?? string.Empty,
                    Pax = b.NumberOfHeads,
                    PackageName = b.Package?.Description,
                    Status = status,
                    BalanceDue = breakdown.RemainingBalance
                }
            };
        }).ToList();

        return Result<List<CalendarEventResponse>>.Success(events);
    }
}
