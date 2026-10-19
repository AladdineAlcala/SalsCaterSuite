using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SalsCaterSuite.Application.Common.Authorization;
using SalsCaterSuite.Application.Features.Bookings.Commands;
using SalsCaterSuite.Application.Features.Bookings.DTOs;
using SalsCaterSuite.Application.Features.Bookings.Queries;
using SalsCaterSuite.Application.Features.Payments.DTOs;

namespace SalsCaterSuite.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class BookingsController : ControllerBase
{
    private readonly CreateBookingCommand _createBooking;
    private readonly CancelBookingCommand _cancelBooking;
    private readonly GetBookingDetailsQuery _getDetails;
    private readonly GetBookingLedgerQuery _getLedger;

    public BookingsController(
        CreateBookingCommand createBooking,
        CancelBookingCommand cancelBooking,
        GetBookingDetailsQuery getDetails,
        GetBookingLedgerQuery getLedger)
    {
        _createBooking = createBooking;
        _cancelBooking = cancelBooking;
        _getDetails = getDetails;
        _getLedger = getLedger;
    }

    /// <summary>GET /api/bookings/{id} — Get booking details</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(BookingDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetDetails(int id, CancellationToken cancellationToken)
    {
        var result = await _getDetails.ExecuteAsync(id, cancellationToken);
        return result.Succeeded
            ? Ok(result.Data)
            : NotFound(new { errors = result.Errors });
    }

    /// <summary>GET /api/bookings/{id}/ledger — Get payment ledger summary</summary>
    [HttpGet("{id:int}/ledger")]
    [ProducesResponseType(typeof(LedgerSummaryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetLedger(int id, CancellationToken cancellationToken)
    {
        var result = await _getLedger.ExecuteAsync(id, cancellationToken);
        return result.Succeeded
            ? Ok(result.Data)
            : NotFound(new { errors = result.Errors });
    }

    /// <summary>POST /api/bookings — Create a new booking</summary>
    [HttpPost]
    [ProducesResponseType(typeof(CreateBookingResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateBookingRequestDto request, CancellationToken cancellationToken)
    {
        var result = await _createBooking.ExecuteAsync(request, cancellationToken);
        if (!result.Succeeded)
        {
            return BadRequest(new { errors = result.Errors });
        }

        return CreatedAtAction(
            nameof(GetDetails),
            new { id = result.Data },
            new CreateBookingResponseDto { TransactionId = result.Data });
    }

    /// <summary>POST /api/bookings/{id}/cancel — Cancel a booking</summary>
    [HttpPost("{id:int}/cancel")]
    [Authorize(Policy = AuthorizationPolicies.BookingsCancel)]
    public async Task<IActionResult> Cancel(int id, [FromBody] CancelBookingRequestDto request, CancellationToken cancellationToken)
    {
        var result = await _cancelBooking.ExecuteAsync(id, request.Reason, request.RefundAmount, cancellationToken);
        return result.Succeeded
            ? Ok(new { message = "Booking cancelled successfully." })
            : BadRequest(new { errors = result.Errors });
    }
}

public record CancelBookingRequestDto(string Reason, decimal? RefundAmount);

