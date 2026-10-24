using SalsCaterSuite.Domain.Enums;

namespace SalsCaterSuite.Application.Features.Addons.DTOs;

/// <summary>
/// One add-on of the administration list (the owner's 2026-10-24 Settings → Add-ons request).
/// <para>
/// Unlike the booking picker's <c>AddonMasterItemDto</c>, this payload includes the
/// <b>inactive</b> rows so an administrator can see and reactivate them, and it does not
/// publish a headcount-dependent line total. Soft-deleted rows are never returned.
/// </para>
/// </summary>
public class AddonAdminItemDto
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

/// <summary>
/// The body of <c>POST /api/addons</c> and <c>PUT /api/addons/{id}</c>.
/// <para>
/// Every member maps 1:1 to <c>AddonDetail</c>. <c>AddonCategoryId</c> and <c>DeptId</c> are
/// optional foreign keys; <c>PricingBasis</c> is optional and <c>null</c> means "not stated"
/// (treated as <c>Flat</c>). <c>IsActive</c> defaults to <c>true</c>.
/// </para>
/// </summary>
public record AddonUpsertRequest
{
    public int? AddonCategoryId { get; init; }
    public int? DeptId { get; init; }
    public AddonPricingBasis? PricingBasis { get; init; }
    public string Description { get; init; } = string.Empty;
    public decimal UnitPrice { get; init; }
    public string? Unit { get; init; }
    public bool IsActive { get; init; } = true;
}

/// <summary>One department of the read-only lookup the add-on form needs.</summary>
public class DepartmentDto
{
    public int DeptId { get; set; }
    public string DeptName { get; set; } = string.Empty;
}
