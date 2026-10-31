using SalsCaterSuite.Domain.Enums;

namespace SalsCaterSuite.Application.Features.Addons.DTOs.Response;

/// <summary>
/// One add-on of the administration list (the owner's 2026-10-24 Settings → Add-ons request).
/// <para>
/// Unlike the booking picker's <c>AddonMasterItemResponse</c>, this payload includes the
/// <b>inactive</b> rows so an administrator can see and reactivate them, and it does not
/// publish a headcount-dependent line total. Soft-deleted rows are never returned.
/// </para>
/// </summary>
public class AddonAdminItemResponse
{
    public int AddonId { get; set; }

    /// <summary><c>null</c> = not stated — an add-on may pre-date the category master.</summary>
    public int? AddonCategoryId { get; set; }

    public string? AddonCategoryName { get; set; }

    /// <summary><c>null</c> = not stated (the owner's 2026-10-24 department request).</summary>
    public int? DeptId { get; set; }

    public string? DeptName { get; set; }

    /// <summary><c>null</c> = not stated, and the server treats it as <c>Flat</c> (DEC-158).</summary>
    public AddonPricingBasis? PricingBasis { get; set; }

    public string Description { get; set; } = string.Empty;

    /// <summary>The <b>unit</b> amount — per guest for <c>PerGuest</c>, the whole line for <c>Flat</c>.</summary>
    public decimal UnitPrice { get; set; }

    /// <summary>The descriptive unit of measure; it never decides the pricing basis.</summary>
    public string? Unit { get; set; }

    public bool IsActive { get; set; }
}
