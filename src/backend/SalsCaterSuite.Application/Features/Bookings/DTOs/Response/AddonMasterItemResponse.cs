using SalsCaterSuite.Domain.Enums;

namespace SalsCaterSuite.Application.Features.Bookings.DTOs.Response;

/// <summary>One row of the add-on master (contract §16.1).</summary>
public class AddonMasterItemResponse
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
