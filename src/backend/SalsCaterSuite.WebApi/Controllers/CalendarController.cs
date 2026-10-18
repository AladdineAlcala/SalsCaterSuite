using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SalsCaterSuite.Application.Common.Interfaces;
using SalsCaterSuite.Application.Features.Calendar.DTOs;
using SalsCaterSuite.Application.Services;

namespace SalsCaterSuite.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CalendarController : ControllerBase
{
    private readonly ISalsCaterSuiteDbContext _context;
    private readonly BookingAmountDueCalculator _calculator;

    public CalendarController(ISalsCaterSuiteDbContext context, BookingAmountDueCalculator calculator)
    {
        _context = context;
        _calculator = calculator;
    }

    /// <summary>GET /api/calendar/events — Get calendar events, optionally filtered by date range</summary>
    [HttpGet("events")]
    public async Task<IActionResult> GetEvents(
        [FromQuery] DateTime? start,
        [FromQuery] DateTime? end,
        CancellationToken cancellationToken)
    {
        var from = start ?? DateTime.UtcNow.AddMonths(-1);
        var to = end ?? DateTime.UtcNow.AddMonths(3);

        var bookings = await _context.Bookings
            .AsNoTracking()
            .Include(b => b.Customer)
            .Include(b => b.Package)
            .Include(b => b.Payments)
            .Where(b =>
                b.IsCancelled != true &&
                b.StartDate.HasValue &&
                b.StartDate.Value >= from &&
                b.StartDate.Value <= to)
            .OrderBy(b => b.StartDate)
            .ToListAsync(cancellationToken);

        var events = bookings.Select(b =>
        {
            var totalPaid = b.Payments?.Sum(p => p.AmountPaid ?? 0m) ?? 0m;
            var breakdown = _calculator.Calculate(
                b.PackageAmount, b.NoOfPerson, b.Package?.AmountPax,
                0m, b.ApplyExtendedAmount ?? false, 0m, 0m, 0m, null, totalPaid);

            var status = b.ServeStatus == true ? "Served"
                       : breakdown.IsFullyPaid ? "Fully Paid"
                       : breakdown.RemainingBalance > 0 ? "With Balance"
                       : "Confirmed";

            return new CalendarEventDto
            {
                Id = b.TransactionId.ToString(),
                Title = $"{b.Customer?.FullName ?? "Unknown"} - {b.Occasion}",
                Start = b.StartDate,
                End = b.EndDate ?? b.StartDate,
                BackgroundColor = b.EventColor ?? "#2563eb",
                BorderColor = b.EventColor ?? "#1d4ed8",
                TextColor = "#ffffff",
                AllDay = false,
                ExtendedProps = new ExtendedEventProps
                {
                    TransactionId = b.TransactionId,
                    CustomerName = b.Customer?.FullName ?? "Unknown",
                    Venue = b.Venue ?? string.Empty,
                    Pax = b.NoOfPerson,
                    PackageName = b.Package?.Description,
                    Status = status,
                    BalanceDue = breakdown.RemainingBalance
                }
            };
        }).ToList();

        return Ok(events);
    }
}

