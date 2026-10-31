using SalsCaterSuite.Application.Common.Interfaces;
using SalsCaterSuite.Application.Common.Models;
using SalsCaterSuite.Application.Features.Bookings.DTOs.Request;
using SalsCaterSuite.Application.Features.Bookings.DTOs.Response;
using SalsCaterSuite.Application.Features.Bookings.Models;
using SalsCaterSuite.Application.Features.Bookings.Services;

namespace SalsCaterSuite.Application.Features.Bookings.Queries;

/// <summary>
/// <c>GET /api/bookings/{id}/menus</c> — the booking's dish selection, the tier's ruleset with
/// its <b>live</b> qualified pools, and the menu-change window (contract §15.1, REQ-BKG-06).
/// <para>
/// The selection carries <b>no money</b> (BR-37): the two planning figures it publishes come from
/// the manually entered catalogue food costs and never enter a booking total. The lock date is
/// always a concrete date; only the <b>days</b> figure is <c>null</c> when the configuration key
/// is absent (DEC-128).
/// </para>
/// </summary>
public class GetBookingMenuSelectionQuery
{
    private readonly IBookingRepository _bookings;
    private readonly BookingMenuSelectionPresenter _presenter;

    public GetBookingMenuSelectionQuery(
        IBookingRepository bookings,
        BookingMenuSelectionPresenter presenter)
    {
        _bookings = bookings;
        _presenter = presenter;
    }

    public async Task<Result<BookingMenuSelectionResponse>> ExecuteAsync(
        int transactionId,
        CancellationToken cancellationToken = default)
    {
        var booking = await _bookings.GetWithLineItemsAsync(transactionId, cancellationToken);

        if (booking is null)
        {
            return new BookingNotFoundResult<BookingMenuSelectionResponse>("Booking not found.");
        }

        return Result<BookingMenuSelectionResponse>.Success(
            await _presenter.BuildAsync(booking, cancellationToken));
    }
}
