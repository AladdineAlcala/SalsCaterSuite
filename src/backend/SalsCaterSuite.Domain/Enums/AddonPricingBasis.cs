namespace SalsCaterSuite.Domain.Enums;

/// <summary>
/// An add-on's explicit pricing basis (REQ-BKG-07, DEC-158).
/// <para>
/// The basis is a <b>column</b>, never a test on the free-text unit of measure: a silent
/// mismatch between "pax" and "per pax" would charge 150.00 where 18,000.00 was meant.
/// <c>null</c> means <b>not stated</b> and is treated as <see cref="Flat"/> — the safe
/// direction, because it never multiplies a price that was not meant to be multiplied.
/// </para>
/// </summary>
public enum AddonPricingBasis
{
    /// <summary>The catalogue amount is the whole line (value <c>0</c>, per the data dictionary).</summary>
    Flat = 0,

    /// <summary>The catalogue amount is per guest; the line total is the amount times the headcount.</summary>
    PerGuest = 1
}
