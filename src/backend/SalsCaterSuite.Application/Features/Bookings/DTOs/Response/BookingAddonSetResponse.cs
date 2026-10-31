using SalsCaterSuite.Domain.Enums;

namespace SalsCaterSuite.Application.Features.Bookings.DTOs.Response;

/// <summary>
/// A booking's add-on lines and the menu-change window (contract §16.3, REQ-BKG-07, BR-39).
/// <para>
/// <c>description</c>, <c>amount</c>, <c>unit</c> and <c>pricingBasis</c> are the <b>frozen</b>
/// line values. <c>amount</c> is the <b>line total</b> — what joins the gross (DEC-158).
/// </para>
/// </summary>
public class BookingAddonSetResponse
{
    public int TransactionId { get; set; }
    public string BookingNumber { get; set; } = string.Empty;
    public DateOnly EventDate { get; set; }

    /// <summary>Always a concrete date — never <c>null</c> (DEC-128).</summary>
    public DateOnly MenuSelectionLockDate { get; set; }

    /// <summary><c>null</c> when the lock-days configuration is absent.</summary>
    public int? MenuSelectionLockDays { get; set; }

    public bool IsLocked { get; set; }

    /// <summary>The frozen line total that joins the gross.</summary>
    public decimal AddonsTotal { get; set; }

    public List<BookingAddonLineResponse> Addons { get; set; } = new();
}
