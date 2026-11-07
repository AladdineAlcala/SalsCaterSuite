using SalsCaterSuite.Application.Features.Payments.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QuestPDF.Fluent;
using SalsCaterSuite.Application.Common.Authorization;
using SalsCaterSuite.Application.Features.Bookings;
using SalsCaterSuite.Application.Features.Bookings.Commands;
using SalsCaterSuite.Application.Features.Bookings.DTOs.Request;
using SalsCaterSuite.Application.Features.Bookings.DTOs.Response;
using SalsCaterSuite.Application.Features.Bookings.Models;
using SalsCaterSuite.Application.Features.Bookings.Queries;
using SalsCaterSuite.Application.Features.Payments.DTOs.Request;
using SalsCaterSuite.Application.Features.Payments.DTOs.Response;
using SalsCaterSuite.Domain.Enums;
using SalsCaterSuite.Infrastructure.Documents;
using SalsCaterSuite.Infrastructure.Exports;

namespace SalsCaterSuite.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class BookingsController : ControllerBase
{
    private readonly CreateBookingCommand _createBooking;
    private readonly CancelBookingCommand _cancelBooking;
    private readonly UpdateBookingCoreCommand _updateBookingCore;
    private readonly GetBookingDetailsQuery _getDetails;
    private readonly GetBookingLedgerQuery _getLedger;
    private readonly GetBookingRegisterQuery _getRegister;
    private readonly GetBookingRegisterSummaryQuery _getSummary;
    private readonly PreviewBookingQuoteCommand _quoteBooking;
    private readonly BookingRegisterExport _registerExport;
    private readonly GetBookingMenuSelectionQuery _getMenuSelection;
    private readonly ReplaceBookingMenuSelectionCommand _replaceMenuSelection;
    private readonly GetBookingAddonSetQuery _getAddonSet;
    private readonly ReplaceBookingAddonsCommand _replaceAddons;

    public BookingsController(
        CreateBookingCommand createBooking,
        CancelBookingCommand cancelBooking,
        UpdateBookingCoreCommand updateBookingCore,
        GetBookingDetailsQuery getDetails,
        GetBookingLedgerQuery getLedger,
        GetBookingRegisterQuery getRegister,
        GetBookingRegisterSummaryQuery getSummary,
        PreviewBookingQuoteCommand quoteBooking,
        BookingRegisterExport registerExport,
        GetBookingMenuSelectionQuery getMenuSelection,
        ReplaceBookingMenuSelectionCommand replaceMenuSelection,
        GetBookingAddonSetQuery getAddonSet,
        ReplaceBookingAddonsCommand replaceAddons)
    {
        _createBooking = createBooking;
        _cancelBooking = cancelBooking;
        _updateBookingCore = updateBookingCore;
        _getDetails = getDetails;
        _getLedger = getLedger;
        _getRegister = getRegister;
        _getSummary = getSummary;
        _quoteBooking = quoteBooking;
        _registerExport = registerExport;
        _getMenuSelection = getMenuSelection;
        _replaceMenuSelection = replaceMenuSelection;
        _getAddonSet = getAddonSet;
        _replaceAddons = replaceAddons;
    }

    /// <summary>GET /api/bookings/{id} — Get booking details</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(BookingResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetDetails(int id, CancellationToken cancellationToken)
    {
        var result = await _getDetails.ExecuteAsync(id, cancellationToken);
        return result.Succeeded
            ? Ok(result.Data)
            : NotFound(new { errors = result.Errors });
    }

    /// <summary>PUT /api/bookings/{id} — update editable booking core details.</summary>
    [HttpPut("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateCore(int id, [FromBody] UpdateBookingCoreRequest request, CancellationToken cancellationToken)
    {
        var result = await _updateBookingCore.ExecuteAsync(id, request, cancellationToken);

        if (result.Succeeded)
        {
            return NoContent();
        }

        if (result.Errors.Contains("Booking not found."))
        {
            return NotFound(new { errors = result.Errors });
        }

        return BadRequest(new { errors = result.Errors });
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

    /// <summary>
    /// GET /api/bookings — the Bookings &amp; Events Register: the paged, filtered dossier with
    /// its totals strip and its facet counts (contract §14.1).
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(BookingRegisterPageResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetRegister(
        [FromQuery] string? search,
        [FromQuery] string? status,
        [FromQuery] string? packageType,
        [FromQuery] string? venue,
        [FromQuery] string? paymentState,
        [FromQuery] bool? overdue,
        [FromQuery] DateOnly? fromDate,
        [FromQuery] DateOnly? toDate,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? sort = null,
        CancellationToken cancellationToken = default)
    {
        var result = await _getRegister.ExecuteAsync(
            BuildFilter(search, status, packageType, venue, paymentState, overdue, fromDate, toDate, page, pageSize, sort),
            cancellationToken);

        return result.Succeeded
            ? Ok(result.Data)
            : BadRequest(new { errors = result.Errors });
    }

    /// <summary>
    /// GET /api/bookings/summary — the register aggregate: the KPI cards, the payment-state
    /// distribution and the capacity load (contract §14.2).
    /// </summary>
    [HttpGet("summary")]
    [ProducesResponseType(typeof(BookingRegisterSummaryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetSummary(
        [FromQuery] string? search,
        [FromQuery] string? status,
        [FromQuery] string? packageType,
        [FromQuery] string? venue,
        [FromQuery] string? paymentState,
        [FromQuery] bool? overdue,
        [FromQuery] DateOnly? fromDate,
        [FromQuery] DateOnly? toDate,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? sort = null,
        CancellationToken cancellationToken = default)
    {
        var result = await _getSummary.ExecuteAsync(
            BuildFilter(search, status, packageType, venue, paymentState, overdue, fromDate, toDate, page, pageSize, sort),
            cancellationToken);

        return result.Succeeded
            ? Ok(result.Data)
            : BadRequest(new { errors = result.Errors });
    }

    /// <summary>
    /// GET /api/bookings/export — the register XLSX: the <b>whole filtered population</b>, never
    /// the current page (contract §14.6).
    /// </summary>
    [HttpGet("export")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ExportRegister(
        [FromQuery] string? search,
        [FromQuery] string? status,
        [FromQuery] string? packageType,
        [FromQuery] string? venue,
        [FromQuery] string? paymentState,
        [FromQuery] bool? overdue,
        [FromQuery] DateOnly? fromDate,
        [FromQuery] DateOnly? toDate,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? sort = null,
        CancellationToken cancellationToken = default)
    {
        var result = await _getRegister.ExecuteUnpagedAsync(
            BuildFilter(search, status, packageType, venue, paymentState, overdue, fromDate, toDate, page, pageSize, sort),
            cancellationToken);

        if (!result.Succeeded)
        {
            return BadRequest(new { errors = result.Errors });
        }

        var bytes = _registerExport.Generate(result.Data!.Items, result.Data.Totals);

        return File(
            bytes,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"BookingsRegister-{DateTime.Now:yyyyMMdd}.xlsx");
    }

    /// <summary>
    /// POST /api/bookings/quote — the server-computed quote for the Reservation Workbench. It
    /// persists nothing (contract §14.3, BR-31).
    /// </summary>
    [HttpPost("quote")]
    [ProducesResponseType(typeof(BookingQuoteResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Quote(
        [FromBody] BookingQuoteRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await _quoteBooking.ExecuteAsync(request, cancellationToken);

        return result.Succeeded
            ? Ok(result.Data)
            : BadRequest(new { errors = result.Errors });
    }

    /// <summary>
    /// GET /api/bookings/{id}/contract — the printable catering contract (contract §14.5).
    /// <para>
    /// The document is composed from the DTO the <b>one</b> booking-details path produces, so
    /// its figures are the shared calculator's — it can never disagree with the ledger it sits
    /// beside (DEC-113).
    /// </para>
    /// </summary>
    [HttpGet("{id:int}/contract")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetContract(int id, CancellationToken cancellationToken)
    {
        var result = await _getDetails.ExecuteAsync(id, cancellationToken);

        if (!result.Succeeded)
        {
            return NotFound(new { errors = result.Errors });
        }

        var document = new ContractDocument(result.Data!);
        var bytes = document.GeneratePdf();

        return File(bytes, "application/pdf", $"Contract-{id}-{DateTime.Now:yyyyMMdd}.pdf");
    }

    /// <summary>
    /// GET /api/bookings/{id}/menus — the booking's dish selection, the tier's ruleset with its
    /// live qualified pools, and the menu-change window (contract §15.1, REQ-BKG-06).
    /// </summary>
    [HttpGet("{id:int}/menus")]
    [ProducesResponseType(typeof(BookingMenuSelectionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMenuSelection(int id, CancellationToken cancellationToken)
    {
        var result = await _getMenuSelection.ExecuteAsync(id, cancellationToken);

        return result.Succeeded
            ? Ok(result.Data)
            : NotFound(new { errors = result.Errors });
    }

    /// <summary>
    /// PUT /api/bookings/{id}/menus — replace the booking's dish selection (contract §15.2,
    /// DEC-134). An explicitly empty array clears the selection, subject to the lock; a
    /// <c>null</c> member is a <c>400</c>.
    /// </summary>
    [HttpPut("{id:int}/menus")]
    [ProducesResponseType(typeof(BookingMenuSelectionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ReplaceMenuSelection(
        int id,
        [FromBody] BookingMenuSelectionRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _replaceMenuSelection.ExecuteAsync(id, request, cancellationToken);

        if (result.Succeeded)
        {
            return Ok(result.Data);
        }

        return result is BookingNotFoundResult<BookingMenuSelectionResponse>
            ? NotFound(new { errors = result.Errors })
            : BadRequest(new { errors = result.Errors });
    }

    /// <summary>
    /// GET /api/bookings/{id}/addons — the booking's frozen add-on lines and the menu-change
    /// window (contract §16.3, REQ-BKG-07).
    /// </summary>
    [HttpGet("{id:int}/addons")]
    [ProducesResponseType(typeof(BookingAddonSetResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAddonSet(int id, CancellationToken cancellationToken)
    {
        var result = await _getAddonSet.ExecuteAsync(id, cancellationToken);

        return result.Succeeded
            ? Ok(result.Data)
            : NotFound(new { errors = result.Errors });
    }

    /// <summary>
    /// PUT /api/bookings/{id}/addons — replace the booking's add-on lines and <b>re-freeze</b>
    /// the money (contract §16.4, DEC-152).
    /// </summary>
    [HttpPut("{id:int}/addons")]
    [ProducesResponseType(typeof(BookingAddonSetResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ReplaceAddons(
        int id,
        [FromBody] BookingAddonsRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _replaceAddons.ExecuteAsync(id, request, cancellationToken);

        if (result.Succeeded)
        {
            return Ok(result.Data);
        }

        return result is BookingNotFoundResult<BookingAddonSetResponse>
            ? NotFound(new { errors = result.Errors })
            : BadRequest(new { errors = result.Errors });
    }

    /// <summary>
    /// GET /api/bookings/{id}/beo — the Kitchen Banquet Event Order (contract §15.5, DEC-148,
    /// DEC-159). It renders the booking's <b>selection</b>, its per-event prep instructions and
    /// its add-on lines; with no selection it prints the tier's course rules and states that the
    /// event runs the standard run.
    /// </summary>
    [HttpGet("{id:int}/beo")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetBeo(int id, CancellationToken cancellationToken)
    {
        var details = await _getDetails.ExecuteAsync(id, cancellationToken);

        if (!details.Succeeded)
        {
            return NotFound(new { errors = details.Errors });
        }

        var selection = await _getMenuSelection.ExecuteAsync(id, cancellationToken);

        if (!selection.Succeeded)
        {
            return NotFound(new { errors = selection.Errors });
        }

        var addons = await _getAddonSet.ExecuteAsync(id, cancellationToken);

        if (!addons.Succeeded)
        {
            return NotFound(new { errors = addons.Errors });
        }

        var document = new KitchenBeoDocument(details.Data!, selection.Data!, addons.Data!);
        var bytes = document.GeneratePdf();

        return File(bytes, "application/pdf", $"BEO-{id}-{DateTime.Now:yyyyMMdd}.pdf");
    }

    /// <summary>
    /// POST /api/bookings — create a draft or a confirmed booking, with an optional initial
    /// payment (contract §14.4, BR-33).
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(CreateBookingResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Create([FromBody] CreateBookingRequest request, CancellationToken cancellationToken)
    {
        // The authorization is two-level (BR-12, contract §14.4): a draft needs only an
        // authenticated user, but a confirmed booking that carries an initial payment
        // additionally requires the Payments.Record policy.
        if (request.InitialPayment is not null && !HasPaymentsRecordRole())
        {
            return StatusCode(
                StatusCodes.Status403Forbidden,
                new { errors = new[] { "You are not authorized to perform this action." } });
        }

        var result = await _createBooking.ExecuteAsync(request, cancellationToken);
        if (!result.Succeeded)
        {
            return BadRequest(new { errors = result.Errors });
        }

        var created = await _getDetails.ExecuteAsync(result.Data, cancellationToken);

        return CreatedAtAction(
            nameof(GetDetails),
            new { id = result.Data },
            new CreateBookingResponse
            {
                TransactionId = result.Data,
                BookingNumber = created.Data?.BookingNumber ?? string.Empty,
                Status = created.Data?.Status ?? BookingStatus.Pending,
                PaymentState = created.Data?.PaymentState ?? PaymentState.PendingPayment,
                EventEndDate = created.Data?.EventEndDate ?? default
            });
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

    /// <summary>
    /// Whether the caller holds the <c>Payments.Record</c> policy. The role names come from the
    /// canonical vocabulary in the Application layer (DEC-017), so the policy and the check can
    /// never drift apart.
    /// </summary>
    private bool HasPaymentsRecordRole() =>
        AuthorizationPolicies.PaymentsRecordRoles.Any(role => User.IsInRole(role));

    /// <summary>
    /// Binds the register's query string onto the filter request. The vocabulary is checked by
    /// the Application layer, so an unknown status or payment-state name is a 400 (BR-32).
    /// </summary>
    private static BookingRegisterFilterRequest BuildFilter(
        string? search,
        string? status,
        string? packageType,
        string? venue,
        string? paymentState,
        bool? overdue,
        DateOnly? fromDate,
        DateOnly? toDate,
        int page,
        int pageSize,
        string? sort) => new()
    {
        Search = search,
        Status = status,
        PackageType = packageType,
        Venue = venue,
        PaymentState = paymentState,
        Overdue = overdue,
        FromDate = fromDate,
        ToDate = toDate,
        Page = page,
        PageSize = pageSize,
        Sort = sort
    };
}

public record CancelBookingRequestDto(string Reason, decimal? RefundAmount);
