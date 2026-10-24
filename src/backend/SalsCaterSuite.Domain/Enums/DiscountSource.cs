namespace SalsCaterSuite.Domain.Enums;

/// <summary>
/// The <b>derived</b> source of a booking's discount (BR-44, DEC-154).
/// <para>
/// The source is never stored: it is derived by an exact rule — a discount code was applied,
/// otherwise the automatic headcount band qualified, otherwise nothing. It is published so
/// KPI-42 is server-computed rather than guessed in the browser.
/// </para>
/// </summary>
public enum DiscountSource
{
    /// <summary>No discount was applied.</summary>
    None = 0,

    /// <summary>The automatic headcount-band discount applied (BR-38).</summary>
    HeadcountBand = 1,

    /// <summary>An explicit discount code applied and replaced the band (DEC-138).</summary>
    Code = 2
}
