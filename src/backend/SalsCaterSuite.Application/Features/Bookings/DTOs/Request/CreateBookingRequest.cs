using SalsCaterSuite.Domain.Enums;

namespace SalsCaterSuite.Application.Features.Bookings.DTOs.Request;

/// <summary>
/// Request body for POST /api/bookings.
/// <para>
/// The body was extended by REQ-BKG-05 (contract §14.4): the dispatch schedule, the discount
/// code, the deposit, the <c>confirm</c> switch and the optional initial payment were added to
/// the existing fields. Every extension is <b>additive</b> — a payload that omits them behaves
/// exactly as before.
/// </para>
/// </summary>
public class CreateBookingRequest
{
    public int CustomerId { get; set; }
    public BookingType BookType { get; set; } = BookingType.Inside;
    public ServiceType TypeOfService { get; set; } = ServiceType.Catering;
    public int NoOfPerson { get; set; }
    public string Occasion { get; set; } = string.Empty;
    public string Venue { get; set; } = string.Empty;
    /// <summary>
    /// The scheduled start as an Asia/Manila wall-clock instant. The payload must carry an
    /// explicit <c>+08:00</c> offset (DEC-178 §8.1); a missing or different offset is a 400.
    /// </summary>
    public DateTimeOffset StartDate { get; set; }

    /// <summary>
    /// The scheduled end as an Asia/Manila wall-clock instant. The payload must carry an
    /// explicit <c>+08:00</c> offset (DEC-178 §8.1); a missing or different offset is a 400.
    /// The date component preserves an overnight event's end day.
    /// </summary>
    public DateTimeOffset EndDate { get; set; }
    public string? EventColor { get; set; }
    public int? PackageId { get; set; }
    public string? Reference { get; set; }
    public int? ExtendedAreaId { get; set; }
    public bool ApplyExtendedAmount { get; set; }
    public string? SpecialInstructions { get; set; }
    /// <summary>
    /// Optional override for the package price per head. When null the system
    /// reads AmountPax from the selected Package record at the time of creation.
    /// </summary>
    public decimal? OverridePricePerHead { get; set; }

    // ─────────────── REQ-BKG-05 extensions (contract §14.4) ───────────────

    /// <summary>The kitchen call time (DEC-106).</summary>
    public TimeSpan? CallTime { get; set; }

    /// <summary>The pack-out time (DEC-106).</summary>
    public TimeSpan? PackoutTime { get; set; }

    /// <summary>The discount code to resolve against the discount catalogue (BR-02).</summary>
    public string? DiscountCode { get; set; }

    /// <summary>
    /// The deposit the operator agreed (BR-30). Advisory — it is never a second payment and it
    /// never changes the balance.
    /// </summary>
    public decimal? DepositAmount { get; set; }

    /// <summary>
    /// <c>false</c> creates a <b>draft</b> in the <c>Pending</c> status; <c>true</c> creates a
    /// <c>Confirmed</c> booking (BR-33, DEC-107). No draft entity and no new lifecycle status
    /// are introduced.
    /// </summary>
    public bool Confirm { get; set; }

    /// <summary>
    /// The initial payment, recorded in the <b>same commit</b> as the booking. Only a
    /// <b>confirmed</b> booking may carry one.
    /// </summary>
    public BookingInitialPaymentRequest? InitialPayment { get; set; }

    // ─────────────── REQ-BKG-06/07 extensions (contract §15.3, §16.4) ───────────────

    /// <summary>
    /// The optional dish selection, validated by the <b>same</b> BR-15 rule as
    /// <c>PUT /api/bookings/{id}/menus</c> and stored in the same commit (DEC-134). An
    /// <b>absent</b> member means no selection; an explicitly <b>empty</b> array is a <c>400</c>
    /// — there is nothing to clear on a booking that does not exist yet.
    /// </summary>
    public List<BookingMenuSelectionEntryRequest>? Selections { get; set; }

    /// <summary>
    /// The optional add-on lines, validated by the <b>same</b> rules as
    /// <c>PUT /api/bookings/{id}/addons</c> and stored in the same commit (DEC-151, DEC-158).
    /// </summary>
    public List<BookingAddonRequest>? Addons { get; set; }
}
