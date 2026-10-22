using SalsCaterSuite.Application.Common.Interfaces;
using SalsCaterSuite.Application.Features.Bookings.Models;
using SalsCaterSuite.Application.Services;

namespace SalsCaterSuite.Application.Features.Bookings.Services;

/// <summary>
/// The one loader of the register's population (spec §3.3).
/// <para>
/// The list, the totals and the summary describe the <b>same</b> population, so the load and the
/// row derivation live here and are called from both use cases — the page and the summary can
/// therefore never describe two different books of work.
/// </para>
/// </summary>
public class BookingRegisterPopulationService
{
    private readonly IBookingRepository _bookings;
    private readonly BookingAmountDueCalculator _calculator;

    public BookingRegisterPopulationService(
        IBookingRepository bookings,
        BookingAmountDueCalculator calculator)
    {
        _bookings = bookings;
        _calculator = calculator;
    }

    /// <summary>
    /// The base population — the bookings matching the search and the date range — with every
    /// row's figures derived through the one calculator and the one classifier.
    /// </summary>
    public async Task<List<BookingRegisterRow>> LoadAsync(
        BookingRegisterFilter filter,
        DateOnly today,
        CancellationToken cancellationToken = default)
    {
        var bookings = await _bookings.GetRegisterPopulationAsync(
            filter.Search, filter.FromDate, filter.ToDate, cancellationToken);

        return bookings
            .Select(booking =>
            {
                var collected = BookingPaymentPopulation.Collected(booking);
                var breakdown = _calculator.Calculate(booking, collected);
                return BookingRegisterProjection.Project(booking, breakdown, today);
            })
            .ToList();
    }

    /// <summary>
    /// The bookings of a date window — the preceding-period comparison of the leading tier
    /// (KPI-34). The same derivation is applied, so the two periods are measured identically.
    /// </summary>
    public async Task<List<BookingRegisterRow>> LoadWindowAsync(
        string? search,
        DateOnly fromDate,
        DateOnly toDate,
        DateOnly today,
        CancellationToken cancellationToken = default)
    {
        var bookings = await _bookings.GetRegisterPopulationAsync(
            search, fromDate, toDate, cancellationToken);

        return bookings
            .Select(booking =>
            {
                var collected = BookingPaymentPopulation.Collected(booking);
                var breakdown = _calculator.Calculate(booking, collected);
                return BookingRegisterProjection.Project(booking, breakdown, today);
            })
            .ToList();
    }
}
