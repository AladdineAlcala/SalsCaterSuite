namespace SalsCaterSuite.UnitTests.Application.Services;

using FluentAssertions;
using SalsCaterSuite.Application.Services;
using Xunit;

public class BookingAmountDueCalculatorTests
{
    private readonly BookingAmountDueCalculator _calculator;

    public BookingAmountDueCalculatorTests()
    {
        _calculator = new BookingAmountDueCalculator();
    }

    [Fact]
    public void Calculate_WithPackageAmount_ShouldUseDirectPackageAmount()
    {
        // Arrange
        decimal packageAmount = 50000m;

        // Act
        var result = _calculator.Calculate(
            packageAmount: packageAmount,
            numberOfHeads: 100,
            pricePerPax: 450m,
            extendedAreaRate: 0m,
            applyExtendedArea: false,
            addonsTotal: 0m,
            otherChargesTotal: 0m,
            discountPercentage: 0m,
            discountFixedAmount: null,
            totalPaymentsReceived: 0m
        );

        // Assert
        result.BasePackageAmount.Should().Be(50000m);
        result.GrossAmount.Should().Be(50000m);
        result.NetAmountDue.Should().Be(50000m);
        result.RemainingBalance.Should().Be(50000m);
        result.IsFullyPaid.Should().BeFalse();
    }

    [Fact]
    public void Calculate_WithoutPackageAmount_ShouldComputeFromPricePerPaxAndNoOfPersons()
    {
        // Act (150 persons * 450 per pax = 67,500)
        var result = _calculator.Calculate(
            packageAmount: null,
            numberOfHeads: 150,
            pricePerPax: 450m,
            extendedAreaRate: 0m,
            applyExtendedArea: false,
            addonsTotal: 0m,
            otherChargesTotal: 0m,
            discountPercentage: 0m,
            discountFixedAmount: null,
            totalPaymentsReceived: 0m
        );

        // Assert
        result.BasePackageAmount.Should().Be(67500m);
        result.GrossAmount.Should().Be(67500m);
    }

    [Fact]
    public void Calculate_WithExtendedAreaAndAddons_ShouldSumCorrectlyIntoGross()
    {
        // Act
        var result = _calculator.Calculate(
            packageAmount: 40000m,
            numberOfHeads: 0,
            pricePerPax: 0m,
            extendedAreaRate: 5000m,
            applyExtendedArea: true,
            addonsTotal: 7500m,
            otherChargesTotal: 2500m,
            discountPercentage: 0m,
            discountFixedAmount: null,
            totalPaymentsReceived: 0m
        );

        // Assert: 40000 + 5000 + 7500 + 2500 = 55,000
        result.ExtendedAreaAmount.Should().Be(5000m);
        result.AddonsAmount.Should().Be(7500m);
        result.OtherChargesAmount.Should().Be(2500m);
        result.GrossAmount.Should().Be(55000m);
        result.NetAmountDue.Should().Be(55000m);
    }

    [Fact]
    public void Calculate_WithPercentageDiscount_ShouldComputeCorrectDiscount()
    {
        // Gross: 100,000, 10% discount -> 10,000 discount, Net: 90,000
        var result = _calculator.Calculate(
            packageAmount: 100000m,
            numberOfHeads: 0,
            pricePerPax: 0m,
            extendedAreaRate: 0m,
            applyExtendedArea: false,
            addonsTotal: 0m,
            otherChargesTotal: 0m,
            discountPercentage: 10m,
            discountFixedAmount: null,
            totalPaymentsReceived: 0m
        );

        result.DiscountAmount.Should().Be(10000m);
        result.NetAmountDue.Should().Be(90000m);
    }

    [Fact]
    public void Calculate_WithFixedDiscount_ShouldPrioritizeFixedDiscountOverPercentage()
    {
        // Gross: 100,000, Fixed: 15,000, Percentage: 10%
        var result = _calculator.Calculate(
            packageAmount: 100000m,
            numberOfHeads: 0,
            pricePerPax: 0m,
            extendedAreaRate: 0m,
            applyExtendedArea: false,
            addonsTotal: 0m,
            otherChargesTotal: 0m,
            discountPercentage: 10m,
            discountFixedAmount: 15000m,
            totalPaymentsReceived: 0m
        );

        result.DiscountAmount.Should().Be(15000m);
        result.NetAmountDue.Should().Be(85000m);
    }

    [Fact]
    public void Calculate_WhenDiscountExceedsGross_ShouldCapDiscountAtGross()
    {
        var result = _calculator.Calculate(
            packageAmount: 20000m,
            numberOfHeads: 0,
            pricePerPax: 0m,
            extendedAreaRate: 0m,
            applyExtendedArea: false,
            addonsTotal: 0m,
            otherChargesTotal: 0m,
            discountPercentage: 0m,
            discountFixedAmount: 25000m,
            totalPaymentsReceived: 0m
        );

        result.DiscountAmount.Should().Be(20000m);
        result.NetAmountDue.Should().Be(0m);
        result.RemainingBalance.Should().Be(0m);
    }

    [Fact]
    public void Calculate_WhenPartiallyPaid_ShouldComputeRemainingBalance()
    {
        var result = _calculator.Calculate(
            packageAmount: 50000m,
            numberOfHeads: 0,
            pricePerPax: 0m,
            extendedAreaRate: 0m,
            applyExtendedArea: false,
            addonsTotal: 0m,
            otherChargesTotal: 0m,
            discountPercentage: 0m,
            discountFixedAmount: null,
            totalPaymentsReceived: 20000m
        );

        result.TotalPayments.Should().Be(20000m);
        result.RemainingBalance.Should().Be(30000m);
        result.IsFullyPaid.Should().BeFalse();
    }

    [Fact]
    public void Calculate_WhenFullyPaid_ShouldSetIsFullyPaidTrueAndRemainingBalanceZero()
    {
        var result = _calculator.Calculate(
            packageAmount: 50000m,
            numberOfHeads: 0,
            pricePerPax: 0m,
            extendedAreaRate: 0m,
            applyExtendedArea: false,
            addonsTotal: 0m,
            otherChargesTotal: 0m,
            discountPercentage: 0m,
            discountFixedAmount: null,
            totalPaymentsReceived: 50000m
        );

        result.RemainingBalance.Should().Be(0m);
        result.IsFullyPaid.Should().BeTrue();
    }
}

