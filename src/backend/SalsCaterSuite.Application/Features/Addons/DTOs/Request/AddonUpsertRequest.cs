using SalsCaterSuite.Domain.Enums;

namespace SalsCaterSuite.Application.Features.Addons.DTOs.Request;

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
