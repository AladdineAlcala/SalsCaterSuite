using SalsCaterSuite.Domain.Common;
using SalsCaterSuite.Domain.Enums;

namespace SalsCaterSuite.Domain.Entities;

/// <summary>
/// One add-on line carried by <b>one booking</b> (REQ-BKG-07, DEC-139, BR-39).
/// <para>
/// The line <b>freezes</b> the description, the <b>line total</b>, the unit and the pricing
/// basis, so a later catalogue price change never moves a stored booking (BR-04). There is no
/// quantity column: the line total is the commercial truth, and for a <see cref="AddonPricingBasis.PerGuest"/>
/// add-on the headcount <b>is</b> the quantity and is expressed within the line (DEC-158).
/// </para>
/// </summary>
public class BookingAddOn : BaseEntity
{
    public int BookingAddOnId { get; set; }

    /// <summary>The booking the line belongs to (<c>trn_Id</c>).</summary>
    public int TransactionId { get; set; }

    public Booking? Booking { get; set; }

    /// <summary>The catalogue add-on the line was drawn from.</summary>
    public int AddonId { get; set; }

    public AddonDetail? AddonDetail { get; set; }

    /// <summary>The <b>frozen</b> description (BR-04).</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// The <b>frozen line total</b> — the line's commercial truth and the amount that joins the
    /// gross (DEC-135, DEC-158). It is the value the server derives or the operator confirms;
    /// it is never multiplied again.
    /// </summary>
    public decimal Amount { get; set; }

    /// <summary>The <b>frozen</b> unit of measure — descriptive only (DEC-158).</summary>
    public string? Unit { get; set; }

    /// <summary>The <b>frozen</b> pricing basis (DEC-158).</summary>
    public AddonPricingBasis PricingBasis { get; set; } = AddonPricingBasis.Flat;
}
