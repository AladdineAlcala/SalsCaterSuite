using SalsCaterSuite.Domain.Common;
using SalsCaterSuite.Domain.Enums;

namespace SalsCaterSuite.Domain.Entities;

public class AddonDetail : BaseEntity
{
    public int AddonId { get; set; }

    /// <summary>
    /// The add-on's category (REQ-BKG-07, DEC-142). <c>null</c> = <b>not stated</b> — an add-on
    /// may pre-date the category master, and it is never a category named "General".
    /// </summary>
    public int? AddonCategoryId { get; set; }

    public AddonCategory? AddonCategory { get; set; }

    /// <summary>
    /// The department that owns the add-on (the owner's 2026-10-24 request). <c>null</c> =
    /// <b>not stated</b> — the legacy add-on rows pre-date the department master.
    /// </summary>
    public int? DeptId { get; set; }

    public Department? Department { get; set; }

    /// <summary>
    /// The explicit pricing basis (<c>0</c> = Flat, <c>1</c> = PerGuest).
    /// <c>null</c> = <b>not stated</b>, and the server treats it as
    /// <see cref="AddonPricingBasis.Flat"/> (DEC-158).
    /// </summary>
    public AddonPricingBasis? PricingBasis { get; set; }

    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// The <b>unit</b> amount — per guest when the basis is
    /// <see cref="AddonPricingBasis.PerGuest"/>, the whole line when it is
    /// <see cref="AddonPricingBasis.Flat"/>.
    /// </summary>
    public decimal UnitPrice { get; set; }

    /// <summary>The unit of measure. <b>Descriptive only</b> — it never decides the basis.</summary>
    public string? Unit { get; set; }

    public bool IsActive { get; set; } = true;

    /// <summary>
    /// The basis the server actually applies: the stated one, or <see cref="AddonPricingBasis.Flat"/>
    /// when the catalogue has not stated one (DEC-158).
    /// </summary>
    public AddonPricingBasis EffectivePricingBasis => PricingBasis ?? AddonPricingBasis.Flat;

    /// <summary>
    /// The line total this catalogue amount would carry on a booking of
    /// <paramref name="numberOfHeads"/> guests: the amount times the headcount for a
    /// <see cref="AddonPricingBasis.PerGuest"/> add-on, the amount itself for a
    /// <see cref="AddonPricingBasis.Flat"/> one (DEC-158).
    /// </summary>
    public decimal LineAmountFor(int numberOfHeads) => EffectivePricingBasis == AddonPricingBasis.PerGuest
        ? UnitPrice * numberOfHeads
        : UnitPrice;
}
