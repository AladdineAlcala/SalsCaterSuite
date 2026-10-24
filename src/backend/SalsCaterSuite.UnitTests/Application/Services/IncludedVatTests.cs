namespace SalsCaterSuite.UnitTests.Application.Services;

using FluentAssertions;
using SalsCaterSuite.Application.Features.Bookings.Services;
using Xunit;

/// <summary>
/// The VAT-inclusive disclosure (BR-42, DEC-143).
/// <para>
/// The price the client pays is VAT-inclusive, so the VAT is a memo figure <b>within</b> the
/// total: <c>net − net / (1 + p/100)</c>. It never adds to the amount due, and it is
/// <c>null</c> when no rate is configured or the net is zero.
/// </para>
/// </summary>
public class IncludedVatTests
{
    [Fact]
    public void An_absent_rate_should_disclose_nothing()
    {
        IncludedVat.Calculate(112_000m, null).Should().BeNull();
    }

    [Fact]
    public void A_zero_net_amount_due_should_disclose_nothing()
    {
        IncludedVat.Calculate(0m, 12m).Should().BeNull();
    }

    [Theory]
    [InlineData(12, 112_000, 12_000.00)]
    [InlineData(12, 56_000, 6_000.00)]
    [InlineData(10, 110_000, 10_000.00)]
    [InlineData(5, 21_000, 1_000.00)]
    public void The_disclosure_should_be_the_net_less_the_net_over_one_plus_the_rate(
        decimal rate,
        decimal net,
        decimal expected)
    {
        IncludedVat.Calculate(net, rate).Should().Be(expected);
    }

    /// <summary>
    /// The formula is applied literally: a configured zero publishes zero, not <c>null</c>. Only
    /// an <b>absent</b> rate is "not stated" (DEC-143).
    /// </summary>
    [Fact]
    public void A_configured_zero_rate_should_publish_zero()
    {
        IncludedVat.Calculate(50_000m, 0m).Should().Be(0m);
    }

    /// <summary>
    /// The disclosure is rounded once, two decimals, away from the midpoint (DEC-115), so the
    /// memo figure is a presentable peso amount.
    /// </summary>
    [Fact]
    public void The_disclosure_should_be_rounded_to_two_decimals()
    {
        // 1,000 − 1,000 / 1.12 = 107.142857...
        IncludedVat.Calculate(1_000m, 12m).Should().Be(107.14m);
    }

    /// <summary>
    /// A nonsensical configuration must not take the surface down: the divisor of exactly zero is
    /// reported as "not stated" rather than thrown.
    /// </summary>
    [Fact]
    public void A_rate_of_minus_one_hundred_should_disclose_nothing()
    {
        IncludedVat.Calculate(10_000m, -100m).Should().BeNull();
    }
}
