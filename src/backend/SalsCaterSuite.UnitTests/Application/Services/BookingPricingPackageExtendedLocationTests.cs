namespace SalsCaterSuite.UnitTests.Application.Services;

using FluentAssertions;
using SalsCaterSuite.Application.Common.Interfaces;
using SalsCaterSuite.Application.Features.Bookings.Services;
using SalsCaterSuite.Application.Services;
using Xunit;

/// <summary>
/// The tier's own per-guest extended-location amount joins the municipality's rate in the one
/// pricing computation (owner request 2026-10-24).
/// </summary>
public class BookingPricingPackageExtendedLocationTests
{
    private readonly BookingPricingService _pricing =
        new(DefaultBookingPolicyProvider.Instance);

    [Fact]
    public void The_tier_amount_is_added_to_the_municipality_rate()
    {
        var result = _pricing.Price(new BookingPricingInput
        {
            PricePerHead = 1000m,
            NumberOfHeads = 100,
            ExtendedAreaRate = 50m,
            PackageExtendedLocationRate = 25m,
            ApplyExtendedAmount = true,
        });

        result.ExtendedAreaRate.Should().Be(50m);
        result.PackageExtendedLocationRate.Should().Be(25m);
        result.ExtendedAreaAmount.Should().Be(7500m);
        result.BaseAmount.Should().Be(100000m);
        result.GrossAmount.Should().Be(107500m);
    }

    [Fact]
    public void The_tier_amount_applies_even_when_no_municipality_is_selected()
    {
        var result = _pricing.Price(new BookingPricingInput
        {
            PricePerHead = 1000m,
            NumberOfHeads = 100,
            ExtendedAreaRate = null,
            PackageExtendedLocationRate = 25m,
            ApplyExtendedAmount = true,
        });

        result.ExtendedAreaAmount.Should().Be(2500m);
        result.GrossAmount.Should().Be(102500m);
    }

    [Fact]
    public void The_operator_switch_still_zeroes_both_components()
    {
        var result = _pricing.Price(new BookingPricingInput
        {
            PricePerHead = 1000m,
            NumberOfHeads = 100,
            ExtendedAreaRate = 50m,
            PackageExtendedLocationRate = 25m,
            ApplyExtendedAmount = false,
        });

        result.ExtendedAreaAmount.Should().Be(0m);
        result.GrossAmount.Should().Be(100000m);
    }

    [Fact]
    public void An_unstated_tier_amount_contributes_nothing()
    {
        var result = _pricing.Price(new BookingPricingInput
        {
            PricePerHead = 1000m,
            NumberOfHeads = 100,
            ExtendedAreaRate = 50m,
            PackageExtendedLocationRate = null,
            ApplyExtendedAmount = true,
        });

        result.PackageExtendedLocationRate.Should().BeNull();
        result.ExtendedAreaAmount.Should().Be(5000m);
    }
}
