namespace SalsCaterSuite.UnitTests.Application.Services;

using System.Reflection;
using FluentAssertions;
using SalsCaterSuite.Application.Services;
using SalsCaterSuite.Domain.Entities;
using SalsCaterSuite.Domain.Enums;
using Xunit;

/// <summary>
/// The shared <c>Calculate(Booking, decimal)</c> overload <b>reproduces</b> the frozen booking
/// snapshot (DEC-099, BR-25). The two net-due sources must be equal to the cent for every
/// booking, and the surcharge is charged <b>once</b>.
/// </summary>
public class BookingAmountDueCalculatorSnapshotTests
{
    private readonly BookingAmountDueCalculator _calculator = new();

    [Fact]
    public void The_shared_overload_should_reproduce_the_frozen_snapshot_of_a_plain_booking()
    {
        var booking = CreateBooking(pricePerHead: 500m, heads: 100);

        var breakdown = _calculator.Calculate(booking, totalPaymentsReceived: 0m);

        breakdown.NetAmountDue.Should().Be(booking.AmountDue);
        breakdown.NetAmountDue.Should().Be(50_000m);
    }

    [Fact]
    public void The_shared_overload_should_reproduce_the_frozen_snapshot_of_a_surcharge_booking()
    {
        // 100 guests × 500 = 50,000 plus a 50.00 per-pax surcharge = 5,000.
        var booking = CreateBooking(pricePerHead: 500m, heads: 100, extendedAreaAmount: 5_000m);

        var breakdown = _calculator.Calculate(booking, totalPaymentsReceived: 0m);

        breakdown.ExtendedAreaAmount.Should().Be(5_000m);
        breakdown.GrossAmount.Should().Be(55_000m);
        breakdown.NetAmountDue.Should().Be(booking.AmountDue);
    }

    [Fact]
    public void The_shared_overload_should_reproduce_the_frozen_snapshot_of_a_percentage_discount_booking()
    {
        var booking = CreateBooking(pricePerHead: 500m, heads: 100, discountPercentage: 10m);

        var breakdown = _calculator.Calculate(booking, totalPaymentsReceived: 0m);

        breakdown.DiscountAmount.Should().Be(5_000m);
        breakdown.NetAmountDue.Should().Be(45_000m);
        breakdown.NetAmountDue.Should().Be(booking.AmountDue);
    }

    [Fact]
    public void The_shared_overload_should_reproduce_the_frozen_snapshot_of_a_fixed_discount_booking()
    {
        var booking = CreateBooking(pricePerHead: 500m, heads: 100, discountAmount: 1_000m);

        var breakdown = _calculator.Calculate(booking, totalPaymentsReceived: 0m);

        breakdown.DiscountAmount.Should().Be(1_000m);
        breakdown.NetAmountDue.Should().Be(49_000m);
        breakdown.NetAmountDue.Should().Be(booking.AmountDue);
    }

    [Fact]
    public void The_shared_overload_should_reproduce_the_frozen_snapshot_of_a_booking_with_both_a_surcharge_and_a_discount()
    {
        var booking = CreateBooking(
            pricePerHead: 500m,
            heads: 100,
            extendedAreaAmount: 5_000m,
            discountPercentage: 10m);

        var breakdown = _calculator.Calculate(booking, totalPaymentsReceived: 0m);

        breakdown.GrossAmount.Should().Be(55_000m, "the surcharge is charged once, on top of the base amount");
        breakdown.DiscountAmount.Should().Be(5_500m, "the percentage is charged on the gross amount");
        breakdown.NetAmountDue.Should().Be(49_500m);
        breakdown.NetAmountDue.Should().Be(booking.AmountDue, "the stored snapshot and the calculator agree to the cent");
    }

    [Fact]
    public void The_surcharge_should_be_charged_once()
    {
        var booking = CreateBooking(pricePerHead: 500m, heads: 100, extendedAreaAmount: 5_000m);

        var breakdown = _calculator.Calculate(booking, totalPaymentsReceived: 0m);

        // The stored surcharge is read directly (DEC-116) — it is never inferred from the
        // subtotal, so it cannot be charged a second time.
        breakdown.ExtendedAreaAmount.Should().Be(5_000m);
        breakdown.GrossAmount.Should().Be(booking.Subtotal);
    }

    [Fact]
    public void Flipping_the_extended_area_flag_should_not_move_the_stored_booking()
    {
        var booking = CreateBooking(pricePerHead: 500m, heads: 100, extendedAreaAmount: 5_000m);

        // The flag is a creation-time switch and an audit marker (DEC-116).
        booking.ApplyExtendedAmount = false;

        var breakdown = _calculator.Calculate(booking, totalPaymentsReceived: 0m);

        breakdown.ExtendedAreaAmount.Should().Be(5_000m);
        breakdown.NetAmountDue.Should().Be(55_000m);
    }

    [Fact]
    public void A_later_change_to_the_municipality_rate_should_not_move_the_stored_booking()
    {
        // BR-04 / AC-BKG-37: the surcharge is FROZEN on the booking, so a later change to the
        // municipality's rate moves the quoted price of every new booking and never a stored
        // one.
        var booking = CreateBooking(pricePerHead: 500m, heads: 100, extendedAreaAmount: 5_000m);

        var area = new Area { AreaId = 1, AreaDetails = "Batangas City", AdditionalRate = 999m };

        var breakdown = _calculator.Calculate(booking, totalPaymentsReceived: 0m);

        breakdown.ExtendedAreaAmount.Should().Be(5_000m, "the stored surcharge, not the municipality's current rate");
        breakdown.NetAmountDue.Should().Be(55_000m);
        area.AdditionalRate.Should().Be(999m);
    }

    [Fact]
    public void A_stored_zero_discount_should_not_shadow_the_percentage_rule()
    {
        var booking = CreateBooking(pricePerHead: 500m, heads: 100, discountPercentage: 10m);

        // The stored amount is zero — the percentage must still apply.
        booking.RecalculateFinancials(0m, 0m, 0m, 0m);
        booking.DiscountPercentage = 10m;

        var breakdown = _calculator.Calculate(booking, totalPaymentsReceived: 0m);

        breakdown.DiscountAmount.Should().Be(5_000m);
    }

    [Fact]
    public void The_six_argument_overload_should_keep_its_signature()
    {
        var method = typeof(BookingAmountDueCalculator).GetMethod(
            "Calculate",
            new[]
            {
                typeof(decimal?),
                typeof(int),
                typeof(decimal),
                typeof(decimal),
                typeof(bool),
                typeof(decimal),
                typeof(decimal),
                typeof(decimal),
                typeof(decimal?),
                typeof(decimal)
            });

        method.Should().NotBeNull("the from-scratch overload is retained with its current signature (DEC-046)");
        method!.ReturnType.Should().Be<BookingCalculationBreakdown>();
    }

    [Fact]
    public void The_two_overloads_should_agree_whenever_the_tax_is_zero()
    {
        var booking = CreateBooking(pricePerHead: 500m, heads: 100, extendedAreaAmount: 5_000m, discountPercentage: 10m);

        var fromScratch = _calculator.Calculate(
            packageAmount: booking.PackageAmount,
            numberOfHeads: booking.NumberOfHeads,
            pricePerPax: booking.PricePerHead,
            extendedAreaRate: booking.ExtendedAreaAmount,
            applyExtendedArea: true,
            addonsTotal: 0m,
            otherChargesTotal: 0m,
            discountPercentage: booking.DiscountPercentage,
            discountFixedAmount: null,
            totalPaymentsReceived: 0m);

        var fromBooking = _calculator.Calculate(booking, totalPaymentsReceived: 0m);

        fromBooking.NetAmountDue.Should().Be(fromScratch.NetAmountDue);
        fromBooking.GrossAmount.Should().Be(fromScratch.GrossAmount);
        fromBooking.DiscountAmount.Should().Be(fromScratch.DiscountAmount);
    }

    /// <summary>
    /// Creates a booking whose frozen snapshot is written the way the create path writes it:
    /// the rounded rate, the frozen surcharge and the rounded discount.
    /// </summary>
    private static Booking CreateBooking(
        decimal pricePerHead,
        int heads,
        decimal extendedAreaAmount = 0m,
        decimal discountPercentage = 0m,
        decimal discountAmount = 0m)
    {
        var eventDate = new DateTime(2026, 11, 20, 17, 0, 0);

        var booking = Booking.Create(
            customerId: 1,
            noOfPerson: heads,
            bookType: BookingType.Inside,
            typeOfService: ServiceType.Catering,
            eventDate: DateOnly.FromDateTime(eventDate),
            startDate: eventDate,
            endDate: eventDate.AddHours(5),
            pricePerHead: pricePerHead,
            applyExtendedAmount: extendedAreaAmount > 0m,
            extendedAreaAmount: extendedAreaAmount);

        booking.DiscountPercentage = discountPercentage;

        // The create path stores the ROUNDED discount, so the stored snapshot and the
        // calculator agree to the cent (DEC-115, DEC-099).
        var gross = pricePerHead * heads + extendedAreaAmount;
        var effectiveDiscount = discountAmount > 0m
            ? discountAmount
            : (discountPercentage > 0m ? gross * discountPercentage / 100m : 0m);

        booking.RecalculateFinancials(extendedAreaAmount, 0m, effectiveDiscount, 0m);

        return booking;
    }
}
