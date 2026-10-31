using SalsCaterSuite.Application.Common.Interfaces;
using SalsCaterSuite.Application.Common.Models;
using SalsCaterSuite.Application.Features.Bookings.DTOs.Request;
using SalsCaterSuite.Application.Features.Bookings.DTOs.Response;
using SalsCaterSuite.Application.Features.Bookings.Models;
using SalsCaterSuite.Application.Features.Bookings.Services;

namespace SalsCaterSuite.Application.Features.Bookings.Queries;

/// <summary>
/// <c>GET /api/bookings/{id}/addons</c> — the booking's frozen add-on lines and the menu-change
/// window (contract §16.3, REQ-BKG-07).
/// </summary>
public class GetBookingAddonSetQuery
{
    private readonly IBookingRepository _bookings;
    private readonly BookingAddonSetPresenter _presenter;

    public GetBookingAddonSetQuery(
        IBookingRepository bookings,
        BookingAddonSetPresenter presenter)
    {
        _bookings = bookings;
        _presenter = presenter;
    }

    public async Task<Result<BookingAddonSetResponse>> ExecuteAsync(
        int transactionId,
        CancellationToken cancellationToken = default)
    {
        var booking = await _bookings.GetWithLineItemsAsync(transactionId, cancellationToken);

        if (booking is null)
        {
            return new BookingNotFoundResult<BookingAddonSetResponse>("Booking not found.");
        }

        return Result<BookingAddonSetResponse>.Success(_presenter.Build(booking));
    }
}
