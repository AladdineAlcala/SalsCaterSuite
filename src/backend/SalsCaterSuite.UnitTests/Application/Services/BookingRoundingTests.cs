namespace SalsCaterSuite.UnitTests.Application.Services;

using FluentAssertions;
using SalsCaterSuite.Application.Services;
using SalsCaterSuite.Domain.Entities;
using SalsCaterSuite.Domain.Enums;
using Xunit;

/// <summary>
/// The <b>one</b> rounding of the money chain (DEC-115, BR-20): two decimals, away from the
/// midpoint, applied once, in the Application layer.
/// <para>
/// The rounding is what makes the stored snapshot and the calculator's figure equal: a
/// percentage discount needs a third decimal (<c>DiscountPercentage</c> is
/// <c>decimal(18,4)</c> while every money column is <c>decimal(18,2)</c>), and an unrounded
/// chain would leave them half a cent apart.
/// </para>
/// </summary>
public class BookingRoundingTests
{
    private readonly BookingAmountDueCalculator _calculator = new();

    [Fact]
    public void Round2_should_round_away_from_the_midpoint()
    {
        BookingAmountDueCalculator.Round2(1.005m).Should().Be(1.01m);
        BookingAmountDueCalculator.Round2(1.015m).Should().Be(1.02m);
        BookingAmountDueCalculator.Round2(1.025m).Should().Be(1.03m);
        BookingAmountDueCalculator.Round2(-1.005m).Should().Be(-1.01m);
    }

    [Fact]
    public void Round2_should_leave_a_two_decimal_value_untouched()
    {
        BookingAmountDueCalculator.Round2(99.99m).Should().Be(99.99m);
        BookingAmountDueCalculator.Round2(0m).Should().Be(0m);
    }

    [Fact]
    public void A_percentage_discount_that_needs_a_third_decimal_should_round_before_it_is_applied()
    {
        // 100.00 gross with a 0.005 % discount is a raw 0.005 — half a cent.
        var breakdown = _calculator.Calculate(
            packageAmount: 100m,
            numberOfHeads: 1,
            pricePerPax: 100m,
            extendedAreaRate: 0m,
            applyExtendedArea: false,
            addonsTotal: 0m,
            otherChargesTotal: 0m,
            discountPercentage: 0.005m,
            discountFixedAmount: null,
            totalPaymentsReceived: 0m);

        breakdown.DiscountAmount.Should().Be(0.01m, "the discount is rounded once, away from the midpoint");
        breakdown.NetAmountDue.Should().Be(99.99m);
    }

    [Fact]
    public void The_stored_and_the_computed_figure_should_be_identical_for_a_third_decimal_discount()
    {
        // The create path stores exactly the rounded values, so the snapshot and the
        // calculator agree to the cent (AC-BKG-33).
        var booking = Booking.Create(
            customerId: 1,
            noOfPerson: 1,
            bookType: BookingType.Inside,
            typeOfService: ServiceType.Catering,
            eventDate: new DateOnly(2026, 11, 20),
            startDate: new DateTime(2026, 11, 20, 17, 0, 0),
            endDate: new DateTime(2026, 11, 20, 22, 0, 0),
            pricePerHead: 100m);

        booking.DiscountPercentage = 0.005m;
        booking.RecalculateFinancials(0m, 0m, 0.01m, 0m);

        var breakdown = _calculator.Calculate(booking, totalPaymentsReceived: 0m);

        breakdown.DiscountAmount.Should().Be(0.01m);
        breakdown.NetAmountDue.Should().Be(99.99m);
        booking.AmountDue.Should().Be(99.99m);
        booking.AmountDue.Should().Be(breakdown.NetAmountDue);
    }

    [Fact]
    public void The_discount_should_be_capped_at_the_gross_amount()
    {
        var breakdown = _calculator.Calculate(
            packageAmount: 100m,
            numberOfHeads: 1,
            pricePerPax: 100m,
            extendedAreaRate: 0m,
            applyExtendedArea: false,
            addonsTotal: 0m,
            otherChargesTotal: 0m,
            discountPercentage: 500m,
            discountFixedAmount: null,
            totalPaymentsReceived: 0m);

        breakdown.DiscountAmount.Should().Be(100m);
        breakdown.NetAmountDue.Should().Be(0m);
    }

    [Fact]
    public void The_remaining_balance_should_be_rounded_once_and_never_negative()
    {
        var breakdown = _calculator.Calculate(
            packageAmount: 100.005m,
            numberOfHeads: 1,
            pricePerPax: 0m,
            extendedAreaRate: 0m,
            applyExtendedArea: false,
            addonsTotal: 0m,
            otherChargesTotal: 0m,
            discountPercentage: 0m,
            discountFixedAmount: null,
            totalPaymentsReceived: 0m);

        breakdown.GrossAmount.Should().Be(100.01m);
        breakdown.RemainingBalance.Should().Be(100.01m);
    }

    [Fact]
    public void The_fixed_discount_should_take_precedence_over_the_percentage()
    {
        var breakdown = _calculator.Calculate(
            packageAmount: 100_000m,
            numberOfHeads: 0,
            pricePerPax: 0m,
            extendedAreaRate: 0m,
            applyExtendedArea: false,
            addonsTotal: 0m,
            otherChargesTotal: 0m,
            discountPercentage: 10m,
            discountFixedAmount: 15_000m,
            totalPaymentsReceived: 0m);

        breakdown.DiscountAmount.Should().Be(15_000m);
        breakdown.NetAmountDue.Should().Be(85_000m);
    }
}
