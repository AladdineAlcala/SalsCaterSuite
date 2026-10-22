namespace SalsCaterSuite.UnitTests.Application.Services;

using System.Reflection;
using FluentAssertions;
using SalsCaterSuite.Application.Services;
using SalsCaterSuite.Domain.Entities;
using SalsCaterSuite.Domain.Enums;
using Xunit;

/// <summary>
/// The shared <c>Calculate(Booking, decimal)</c> overload of the booking calculator
/// (DEC-042). This is the single net-amount-due path the ledger, the booking details, the
/// calendar and the operations dashboard all use, so the argument set is pinned here:
/// the frozen stored <c>PackageAmount</c> is used (never a recomputed pax × heads) and a zero
/// stored discount is converted to <c>null</c> so the percentage rule still applies.
/// </summary>
public class BookingAmountDueCalculatorSharedOverloadTests
{
    private static readonly PropertyInfo StatusProperty =
        typeof(Booking).GetProperty(nameof(Booking.Status))!;

    private readonly BookingAmountDueCalculator _calculator = new();

    [Fact]
    public void The_shared_overload_should_use_the_frozen_package_amount_and_not_recompute_the_pax_total()
    {
        // 10 heads × 500 would be 5,000, but the frozen stored package amount is 50,000.
        var booking = CreateBooking(heads: 10, pricePerHead: 500m, packageAmount: 50_000m);

        var breakdown = _calculator.Calculate(booking, totalPaymentsReceived: 0m);

        breakdown.BasePackageAmount.Should().Be(50_000m, "the frozen stored column is authoritative");
        breakdown.NetAmountDue.Should().Be(50_000m);
    }

    [Fact]
    public void The_shared_overload_should_apply_a_stored_percentage_discount_when_no_fixed_discount_is_stored()
    {
        var booking = CreateBooking(heads: 100, pricePerHead: 500m, packageAmount: 50_000m);
        booking.DiscountPercentage = 10m;

        var breakdown = _calculator.Calculate(booking, totalPaymentsReceived: 0m);

        breakdown.DiscountAmount.Should().Be(5_000m, "a stored zero discount must not shadow the percentage rule");
        breakdown.NetAmountDue.Should().Be(45_000m);
    }

    [Fact]
    public void The_shared_overload_should_give_the_fixed_discount_precedence()
    {
        var booking = CreateBooking(heads: 100, pricePerHead: 500m, packageAmount: 50_000m);
        booking.DiscountPercentage = 10m;
        booking.RecalculateFinancials(0m, 0m, 1_000m, 0m);

        var breakdown = _calculator.Calculate(booking, totalPaymentsReceived: 0m);

        breakdown.DiscountAmount.Should().Be(1_000m, "a stored fixed discount takes precedence (BR-02)");
        breakdown.NetAmountDue.Should().Be(49_000m);
    }

    [Fact]
    public void The_shared_overload_should_clamp_the_remaining_balance_at_zero()
    {
        var booking = CreateBooking(heads: 100, pricePerHead: 500m, packageAmount: 50_000m);

        var breakdown = _calculator.Calculate(booking, totalPaymentsReceived: 60_000m);

        breakdown.TotalPayments.Should().Be(60_000m);
        breakdown.RemainingBalance.Should().Be(0m, "a balance is never negative (BR-03)");
        breakdown.IsFullyPaid.Should().BeTrue();
    }

    [Fact]
    public void The_shared_overload_should_ignore_the_extended_area_flag_because_the_rate_is_zero()
    {
        var booking = CreateBooking(heads: 100, pricePerHead: 500m, packageAmount: 50_000m);
        booking.ApplyExtendedAmount = true;

        var breakdown = _calculator.Calculate(booking, totalPaymentsReceived: 0m);

        breakdown.ExtendedAreaAmount.Should().Be(0m);
        breakdown.NetAmountDue.Should().Be(50_000m);
    }

    private static Booking CreateBooking(int heads, decimal pricePerHead, decimal packageAmount)
    {
        var booking = Booking.Create(
            customerId: 1,
            noOfPerson: heads,
            bookType: BookingType.Inside,
            typeOfService: ServiceType.Catering,
            eventDate: new DateOnly(2026, 10, 19),
            startDate: new DateTime(2026, 10, 19, 17, 0, 0),
            endDate: new DateTime(2026, 10, 19, 22, 0, 0),
            pricePerHead: pricePerHead);

        // The frozen snapshot is what the domain produced; the test overrides it to prove the
        // shared overload reads the stored column rather than recomputing pax × heads.
        typeof(Booking).GetProperty(nameof(Booking.PackageAmount))!.SetValue(booking, packageAmount);
        typeof(Booking).GetProperty(nameof(Booking.AmountDue))!.SetValue(booking, packageAmount);

        StatusProperty.SetValue(booking, BookingStatus.Confirmed);

        return booking;
    }
}
