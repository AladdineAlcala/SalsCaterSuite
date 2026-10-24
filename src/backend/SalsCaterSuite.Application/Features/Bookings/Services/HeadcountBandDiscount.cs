namespace SalsCaterSuite.Application.Features.Bookings.Services;

/// <summary>
/// One headcount band of the automatic discount (BR-38, DEC-136).
/// </summary>
/// <param name="RatePerHead">The fixed amount per booked head.</param>
/// <param name="Label">The server's human-readable description of the band.</param>
public sealed record HeadcountBand(decimal RatePerHead, string Label);

/// <summary>
/// The <b>automatic headcount-band discount</b> (BR-38, DEC-136, DEC-137, DEC-138).
/// <para>
/// The bands are <b>half-open</b> and the 90-99 gap is real: a booking in it earns nothing
/// automatically and is handled by an explicit discount code. The band is a <b>rule</b>, not a
/// row in the <c>Discount</c> catalogue, and it is the <b>only</b> source of a fixed discount
/// that carries no code (DEC-137).
/// </para>
/// </summary>
public static class HeadcountBandDiscount
{
    /// <summary>The label of the 80-89 band.</summary>
    public const string Band80To89Label = "80 to 89 heads";

    /// <summary>The label of the 100-999 band.</summary>
    public const string Band100To999Label = "100 to 999 heads";

    /// <summary>The rate per head of the 80-89 band.</summary>
    public const decimal Band80To89RatePerHead = 10m;

    /// <summary>The rate per head of the 100-999 band.</summary>
    public const decimal Band100To999RatePerHead = 20m;

    /// <summary>
    /// The band that applies to <paramref name="numberOfHeads"/>, or <c>null</c> when the
    /// headcount qualifies for none.
    /// </summary>
    public static HeadcountBand? Resolve(int numberOfHeads) => numberOfHeads switch
    {
        >= 80 and <= 89 => new HeadcountBand(Band80To89RatePerHead, Band80To89Label),
        >= 100 and <= 999 => new HeadcountBand(Band100To999RatePerHead, Band100To999Label),
        _ => null
    };

    /// <summary>
    /// The band's fixed amount — <c>NumberOfHeads x rate</c> — or <b>zero</b> when no band
    /// qualifies.
    /// </summary>
    public static decimal AmountFor(int numberOfHeads)
    {
        var band = Resolve(numberOfHeads);
        return band is null ? 0m : band.RatePerHead * numberOfHeads;
    }
}
