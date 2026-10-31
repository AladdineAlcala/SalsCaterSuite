namespace SalsCaterSuite.UnitTests.Application.Queries;

using FluentAssertions;
using SalsCaterSuite.Application.Features.Bookings.DTOs.Request;
using SalsCaterSuite.Application.Features.Bookings.DTOs.Response;
using SalsCaterSuite.Application.Features.Bookings.Models;
using SalsCaterSuite.Application.Features.Bookings.Queries;
using SalsCaterSuite.Application.Features.Bookings.Services;
using SalsCaterSuite.Application.Features.Bookings.Validators;
using SalsCaterSuite.Application.Services;
using SalsCaterSuite.Domain.Entities;
using SalsCaterSuite.Domain.Enums;
using SalsCaterSuite.UnitTests.Common.Fakes;
using Xunit;

/// <summary>
/// The booking-details projection (<c>GET /api/bookings/{id}</c>).
/// <para>
/// The details endpoint and the register (contract §14.1) must describe <b>the same booking the
/// same way</b>: the net amount due, the collected amount, the outstanding balance, the
/// surcharge, the deposit and the status are identical on both. A declared-but-never-populated
/// field is a silent wrong answer — a consumer would read <c>null</c> for a booking that carries
/// the value — so every field the DTO publishes is asserted here.
/// </para>
/// </summary>
public class GetBookingDetailsQueryTests
{
    private static readonly DateOnly Today = new(2026, 10, 22);

    private readonly FakeSalsCaterSuiteDataStore _store = new();
    private readonly FakeBookingRepository _bookings;
    private readonly FakePackageRepository _packages;
    private readonly FakeMenuRepository _menus;
    private readonly FakePackageCourseRuleRepository _rules;
    private readonly GetBookingDetailsQuery _details;
    private readonly GetBookingRegisterQuery _register;

    public GetBookingDetailsQueryTests()
    {
        _bookings = new FakeBookingRepository(_store);
        _packages = new FakePackageRepository(_store);
        _menus = new FakeMenuRepository(_store);
        _rules = new FakePackageCourseRuleRepository(_store);

        _details = new GetBookingDetailsQuery(_bookings, new BookingAmountDueCalculator());

        _register = new GetBookingRegisterQuery(
            new BookingPlanningEstimateService(
                new BookingTierEstimateService(_packages, _rules, _menus),
                _menus),
            new BookingRegisterPopulationService(
                _bookings,
                new BookingAmountDueCalculator(),
                new FakeBookingPolicyProvider()),
            new FakeDateTime(Today.ToDateTime(TimeOnly.MinValue)),
            new BookingRegisterFilterValidator());
    }

    /// <summary>
    /// A booking that carries the dispatch schedule, the frozen surcharge and a frozen discount
    /// code — the four fields the details projection must publish.
    /// </summary>
    private async Task<Booking> SeedRichBookingAsync(decimal pricePerHead = 500m, int heads = 100)
    {
        _store.Customers.Add(new Customer { CustomerId = 1, FirstName = "Maria", LastName = "Santos" });
        _store.Packages.Add(new Package
        {
            PackageId = 10,
            PackageType = "Premier Buffet",
            Description = "Premier Buffet Tier",
            AmountPax = pricePerHead,
            IsActive = true,
            NumPaxId = 4,
        });

        var eventDate = Today.AddDays(30);

        var booking = Booking.Create(
            customerId: 1,
            noOfPerson: heads,
            bookType: BookingType.Inside,
            typeOfService: ServiceType.Catering,
            eventDate: eventDate,
            startDate: eventDate.ToDateTime(new TimeOnly(17, 0)),
            endDate: eventDate.ToDateTime(new TimeOnly(22, 0)),
            occasion: "Silver Wedding Anniversary",
            venue: "Grand Ballroom",
            packageId: 10,
            packageName: "Premier Buffet Tier",
            pricePerHead: pricePerHead,
            applyExtendedAmount: true,
            callTime: new TimeSpan(14, 0, 0),
            packoutTime: new TimeSpan(23, 0, 0),
            discountCode: "TEN",
            extendedAreaAmount: 5_000m);

        // The frozen money snapshot, written the way the create path writes it.
        booking.DiscountType = DiscountType.Percentage;
        booking.DiscountPercentage = 10m;
        booking.RecalculateFinancials(5_000m, 0m, 5_500m, 0m);
        booking.SetDepositAmount(20_000m);

        await _bookings.AddAsync(booking);
        await _bookings.SaveChangesAsync();
        booking.AssignBookingNumber();

        _store.Payments.Add(new Payment
        {
            PaymentNo = "OR-1",
            TransactionId = booking.TransactionId,
            AmountPaid = 30_000m,
            Particular = "Deposit",
            DateOfPayment = Today.ToDateTime(new TimeOnly(9, 0)),
            PaymentMeans = PaymentMeans.Cash
        });

        return booking;
    }

    [Fact]
    public async Task The_details_should_publish_the_dispatch_schedule_the_surcharge_and_the_discount_code()
    {
        var booking = await SeedRichBookingAsync();

        var result = await _details.ExecuteAsync(booking.TransactionId);

        result.Succeeded.Should().BeTrue();
        var dto = result.Data!;

        dto.CallTime.Should().Be(new TimeSpan(14, 0, 0), "the dispatch schedule is published (DEC-106)");
        dto.PackoutTime.Should().Be(new TimeSpan(23, 0, 0), "the dispatch schedule is published (DEC-106)");
        dto.ExtendedAreaAmount.Should().Be(5_000m, "the frozen surcharge is published (BR-26, DEC-116)");
        dto.DiscountCode.Should().Be("TEN", "the frozen discount code is published (DEC-106, BR-02)");
    }

    [Fact]
    public async Task The_details_should_publish_null_for_the_dispatch_schedule_when_it_was_not_stated()
    {
        var booking = Booking.Create(
            customerId: 1,
            noOfPerson: 50,
            bookType: BookingType.Inside,
            typeOfService: ServiceType.Catering,
            eventDate: Today.AddDays(10),
            startDate: Today.AddDays(10).ToDateTime(new TimeOnly(17, 0)),
            endDate: Today.AddDays(10).ToDateTime(new TimeOnly(22, 0)),
            pricePerHead: 500m);

        await _bookings.AddAsync(booking);
        await _bookings.SaveChangesAsync();

        var result = await _details.ExecuteAsync(booking.TransactionId);

        result.Succeeded.Should().BeTrue();
        result.Data!.CallTime.Should().BeNull("not stated is null, never zero");
        result.Data.PackoutTime.Should().BeNull();
        result.Data.DiscountCode.Should().BeNull();
        result.Data.ExtendedAreaAmount.Should().Be(0m, "no surcharge was applied");
    }

    [Fact]
    public async Task The_details_and_the_register_should_publish_identical_figures_for_the_same_booking()
    {
        var booking = await SeedRichBookingAsync();

        var details = (await _details.ExecuteAsync(booking.TransactionId)).Data!;
        var register = (await _register.ExecuteAsync(new BookingRegisterFilterRequest())).Data!;
        var item = register.Items.Single();

        // The money.
        details.AmountDue.Should().Be(item.NetAmountDue, "the same net amount due on both");
        details.AmountPaid.Should().Be(item.AmountPaid, "the same collected amount on both");
        details.Balance.Should().Be(item.OutstandingBalance, "the same outstanding balance on both");
        details.ExtendedAreaAmount.Should().Be(item.ExtendedAreaAmount, "the same frozen surcharge on both");
        details.DepositAmount.Should().Be(item.DepositAmount, "the same deposit on both");
        details.Subtotal.Should().Be(item.NetAmountDue + item.DiscountAmount, "the same gross on both");

        // The lifecycle.
        details.Status.Should().Be(item.Status, "the same lifecycle status on both");

        // The dispatch schedule and the discount code.
        details.CallTime.Should().Be(item.CallTime);
        details.PackoutTime.Should().Be(item.PackoutTime);
        details.DiscountCode.Should().Be(item.DiscountCode);
    }
}
