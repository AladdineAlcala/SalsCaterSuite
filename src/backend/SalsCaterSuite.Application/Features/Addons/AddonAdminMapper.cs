using SalsCaterSuite.Application.Features.Addons.DTOs.Request;
using SalsCaterSuite.Application.Features.Addons.DTOs.Response;
using SalsCaterSuite.Domain.Entities;

namespace SalsCaterSuite.Application.Features.Addons;

/// <summary>
/// The one mapping from <see cref="AddonDetail"/> to the administration payload, so the list and
/// the detail read can never publish different shapes.
/// </summary>
public static class AddonAdminMapper
{
    public static AddonAdminItemResponse ToDto(AddonDetail addon) => new()
    {
        AddonId = addon.AddonId,
        AddonCategoryId = addon.AddonCategoryId,
        AddonCategoryName = addon.AddonCategory?.AddonCategoryName,
        DeptId = addon.DeptId,
        DeptName = addon.Department?.DeptName,
        PricingBasis = addon.PricingBasis,
        Description = addon.Description,
        UnitPrice = addon.UnitPrice,
        Unit = addon.Unit,
        IsActive = addon.IsActive
    };
}
