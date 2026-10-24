using SalsCaterSuite.Domain.Enums;

namespace SalsCaterSuite.Application.Features.Bookings.DTOs;

/// <summary>
/// A booking's add-on lines and the menu-change window (contract §16.3, REQ-BKG-07, BR-39).
/// <para>
/// <c>description</c>, <c>amount</c>, <c>unit</c> and <c>pricingBasis</c> are the <b>frozen</b>
/// line values. <c>amount</c> is the <b>line total</b> — what joins the gross (DEC-158).
/// </para>
/// </summary>
public class BookingAddonSetDto
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

    public List<BookingAddonLineDto> Addons { get; set; } = new();
}

/// <summary>One frozen add-on line (contract §16.3).</summary>
public class BookingAddonLineDto
{
    public int AddonId { get; set; }

    /// <summary>The frozen description (BR-04).</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>The frozen line total — what joins the gross (DEC-158).</summary>
    public decimal Amount { get; set; }

    /// <summary>The frozen unit of measure — descriptive only.</summary>
    public string? Unit { get; set; }

    /// <summary>The frozen pricing basis (DEC-158).</summary>
    public AddonPricingBasis PricingBasis { get; set; }

    public int? AddonCategoryId { get; set; }
    public string? AddonCategoryName { get; set; }

    /// <summary>The catalogue's current amount; <c>null</c> when the catalogue row is gone.</summary>
    public decimal? CatalogueAmount { get; set; }

    /// <summary>The catalogue's current description; <c>null</c> when the row is gone.</summary>
    public string? CatalogueDescription { get; set; }
}

/// <summary>The request body of <c>PUT /api/bookings/{id}/addons</c> (contract §16.4).</summary>
public class BookingAddonsRequest
{
    /// <summary><c>null</c> is a <c>400</c>; an explicitly empty array clears the lines.</summary>
    public List<BookingAddonRequestDto>? Addons { get; set; }
}

/// <summary>One submitted add-on line. The request carries no quantity and no basis.</summary>
public class BookingAddonRequestDto
{
    /// <summary>Required, and must be an <b>active</b> catalogue row.</summary>
    public int AddonId { get; set; }

    /// <summary>
    /// Optional <b>line total</b> (DEC-158). When absent the server derives
    /// <c>catalogueAmount x heads</c> for a <c>PerGuest</c> add-on and <c>catalogueAmount</c> for
    /// a <c>Flat</c> one.
    /// </summary>
    public decimal? Amount { get; set; }
}

/// <summary>One row of the add-on master (contract §16.1).</summary>
public class AddonMasterItemDto
{
    public int AddonId { get; set; }
    public string Description { get; set; } = string.Empty;

    /// <summary>The catalogue's <b>unit</b> amount — per guest when the basis is <c>PerGuest</c>.</summary>
    public decimal Amount { get; set; }

    /// <summary>The unit of measure; <c>null</c> = not stated. Descriptive only.</summary>
    public string? Unit { get; set; }

    /// <summary><c>null</c> = not stated, and the server treats it as <c>Flat</c> (DEC-158).</summary>
    public AddonPricingBasis? PricingBasis { get; set; }

    /// <summary>The line total this add-on would carry at the given headcount.</summary>
    public decimal LineAmountForBooking { get; set; }

    /// <summary><c>null</c> = not stated — an add-on may pre-date the category master.</summary>
    public int? AddonCategoryId { get; set; }

    public string? AddonCategoryName { get; set; }

    /// <summary>
    /// The owning department (the owner's 2026-10-24 request). <c>null</c> = not stated.
    /// </summary>
    public int? DeptId { get; set; }

    public string? DeptName { get; set; }

    public bool IsActive { get; set; }
}

/// <summary>One row of the add-on category master (contract §16.2).</summary>
public class AddonCategoryDto
{
    public int AddonCategoryId { get; set; }
    public string AddonCategoryName { get; set; } = string.Empty;
}
