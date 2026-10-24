using SalsCaterSuite.Domain.Enums;

namespace SalsCaterSuite.Application.Features.Bookings.Services;

/// <summary>
/// The <b>derived</b> source of a booking's discount (BR-44, DEC-154).
/// <para>
/// It is derived, never stored:
/// <list type="bullet">
/// <item><description>a discount <b>code</b> was applied — <see cref="DiscountSource.Code"/>;</description></item>
/// <item><description>otherwise the <b>headcount band</b> qualified <b>and</b> a discount amount
/// was actually taken — <see cref="DiscountSource.HeadcountBand"/>;</description></item>
/// <item><description>otherwise <see cref="DiscountSource.None"/>.</description></item>
/// </list>
/// The band qualification is part of the derivation because the band is a rule and not a stored
/// row: a booking that pre-dates the rule carries no discount and must not count towards KPI-42.
/// </para>
/// </summary>
public static class DiscountSourceResolver
{
    /// <summary>Derives the source from the frozen booking columns.</summary>
    public static DiscountSource Resolve(string? discountCode, int numberOfHeads, decimal discountAmount)
    {
        if (!string.IsNullOrWhiteSpace(discountCode))
        {
            return DiscountSource.Code;
        }

        return HeadcountBandDiscount.Resolve(numberOfHeads) is not null && discountAmount > 0m
            ? DiscountSource.HeadcountBand
            : DiscountSource.None;
    }

    /// <summary>
    /// The selection state: exactly one of <c>Locked</c>, <c>Selected</c> or <c>None</c>
    /// (BR-28, M2).
    /// </summary>
    public static BookingSelectionState ResolveSelectionState(bool isLocked, bool hasLiveSelection)
    {
        if (isLocked)
        {
            return BookingSelectionState.Locked;
        }

        return hasLiveSelection ? BookingSelectionState.Selected : BookingSelectionState.None;
    }
}
