namespace SalsCaterSuite.UnitTests.Application.Services;

using FluentAssertions;
using SalsCaterSuite.Application.Features.Bookings.Services;
using Xunit;

/// <summary>
/// The automatic headcount-band discount (BR-38, DEC-136, DEC-137, DEC-138).
/// <para>
/// The bands are <b>half-open</b> and the 90-99 gap is real: it earns nothing automatically.
/// The table below is the whole rule, so a boundary can never drift silently.
/// </para>
/// </summary>
public class HeadcountBandDiscountTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(79)]
    [InlineData(90)]
    [InlineData(91)]
    [InlineData(99)]
    [InlineData(1000)]
    [InlineData(5000)]
    public void A_headcount_outside_the_two_bands_should_earn_no_band(int heads)
    {
        HeadcountBandDiscount.Resolve(heads).Should().BeNull();
        HeadcountBandDiscount.AmountFor(heads).Should().Be(0m);
    }

    [Theory]
    [InlineData(80)]
    [InlineData(85)]
    [InlineData(89)]
    public void The_80_to_89_band_should_apply_ten_pesos_per_head(int heads)
    {
        var band = HeadcountBandDiscount.Resolve(heads);

        band.Should().NotBeNull();
        band!.RatePerHead.Should().Be(10m);
        band.Label.Should().Be("80 to 89 heads");
        HeadcountBandDiscount.AmountFor(heads).Should().Be(10m * heads);
    }

    [Theory]
    [InlineData(100)]
    [InlineData(120)]
    [InlineData(999)]
    public void The_100_to_999_band_should_apply_twenty_pesos_per_head(int heads)
    {
        var band = HeadcountBandDiscount.Resolve(heads);

        band.Should().NotBeNull();
        band!.RatePerHead.Should().Be(20m);
        band.Label.Should().Be("100 to 999 heads");
        HeadcountBandDiscount.AmountFor(heads).Should().Be(20m * heads);
    }

    [Fact]
    public void The_labels_should_be_the_ones_the_quote_publishes()
    {
        HeadcountBandDiscount.Band80To89Label.Should().Be("80 to 89 heads");
        HeadcountBandDiscount.Band100To999Label.Should().Be("100 to 999 heads");
        HeadcountBandDiscount.Band80To89RatePerHead.Should().Be(10m);
        HeadcountBandDiscount.Band100To999RatePerHead.Should().Be(20m);
    }

    /// <summary>
    /// The band is a <b>fixed</b> discount — the amount depends on the headcount and never on the
    /// booking's price, so a cheap tier and an expensive one earn the same band (DEC-137).
    /// </summary>
    [Fact]
    public void The_band_should_not_depend_on_the_tier_price()
    {
        HeadcountBandDiscount.AmountFor(120).Should().Be(2_400m);
        HeadcountBandDiscount.AmountFor(80).Should().Be(800m);
    }
}
