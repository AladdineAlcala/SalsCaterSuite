namespace SalsCaterSuite.UnitTests.Application.Queries;

using FluentAssertions;
using FluentValidation;
using SalsCaterSuite.Application.Features.Bookings.DTOs;
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
/// The Bookings &amp; Events Register (REQ-BKG-05, spec §3.3, BR-27, BR-32).
/// <para>
/// The list, the totals and the facets describe the <b>same</b> filtered population; the facet
/// counts are computed with their own dimension removed; and a soft-deleted booking or payment
/// is excluded from every figure.
/// </para>
/// </summary>
public class GetBookingDossierQueryTests
{
    private static readonly DateOnly Today = new(2026, 10, 22);

    private readonly FakeSalsCaterSuiteDataStore _store = new();
    private readonly FakeBookingRepository _bookings;
    private readonly FakePackageRepository _packages;
    private readonly FakeMenuRepository _menus;
    private readonly FakePackageCourseRuleRepository _rules;
    private readonly GetBookingRegisterQuery _query;

    public GetBookingDossierQueryTests()
    {
        _bookings = new FakeBookingRepository(_store);
        _packages = new FakePackageRepository(_store);
        _menus = new FakeMenuRepository(_store);
        _rules = new FakePackageCourseRuleRepository(_store);

        var population = new BookingRegisterPopulationService(
            _bookings,
            new BookingAmountDueCalculator(),
            new FakeBookingPolicyProvider());
        var estimates = new BookingPlanningEstimateService(
            new BookingTierEstimateService(_packages, _rules, _menus),
            _menus);

        _query = new GetBookingRegisterQuery(
            estimates,
            population,
            new FakeDateTime(Today.ToDateTime(TimeOnly.MinValue)),
            new BookingRegisterFilterValidator());
    }

    // ─────────────────────────── the fixtures ───────────────────────────

    private void SeedCustomer(int id, string lastName, string? company = null)
    {
        _store.Customers.Add(new Customer
        {
            CustomerId = id,
            FirstName = "Test",
            LastName = lastName,
            Company = company
        });
    }

    private void SeedPackage(int id, string type, decimal rate)
    {
        _store.Packages.Add(new Package
        {
            PackageId = id,
            PackageType = type,
            Description = $"{type} Tier",
            AmountPax = rate,
            IsActive = true
        });
    }

    private async Task<Booking> SeedBooking(
        int customerId,
        int? packageId,
        DateOnly eventDate,
        string venue,
        decimal rate = 500m,
        int heads = 100,
        BookingStatus? status = null)
    {
        var booking = Booking.Create(
            customerId: customerId,
            noOfPerson: heads,
            bookType: BookingType.Inside,
            typeOfService: ServiceType.Catering,
            eventDate: eventDate,
            startDate: eventDate.ToDateTime(new TimeOnly(17, 0)),
            endDate: eventDate.ToDateTime(new TimeOnly(22, 0)),
            occasion: "Wedding",
            venue: venue,
            packageId: packageId,
            packageName: packageId.HasValue ? "Tier" : null,
            pricePerHead: rate);

        if (status.HasValue)
        {
            typeof(Booking).GetProperty(nameof(Booking.Status))!.SetValue(booking, status.Value);
        }

        await _bookings.AddAsync(booking);
        await _bookings.SaveChangesAsync();

        // The contractual booking number is assigned once the row carries its id (DEC-016).
        booking.AssignBookingNumber();

        return booking;
    }

    private void SeedPayment(Booking booking, decimal amount, bool softDeleted = false)
    {
        _store.Payments.Add(new Payment
        {
            PaymentNo = $"OR-{_store.Payments.Count + 1}",
            TransactionId = booking.TransactionId,
            AmountPaid = amount,
            Particular = "Deposit",
            DateOfPayment = Today.ToDateTime(new TimeOnly(9, 0)),
            PaymentMeans = PaymentMeans.Cash,
            IsDeleted = softDeleted
        });
    }

    // ─────────────────────────── the tests ───────────────────────────

    [Fact]
    public async Task The_register_should_list_the_filtered_population()
    {
        SeedCustomer(1, "Santos");
        SeedPackage(10, "Wedding", 500m);
        await SeedBooking(1, 10, Today.AddDays(5), "Grand Ballroom");
        await SeedBooking(1, 10, Today.AddDays(9), "Grand Ballroom");

        var result = await _query.ExecuteAsync(new BookingRegisterFilterRequest());

        result.Succeeded.Should().BeTrue();
        result.Data!.TotalCount.Should().Be(2);
        result.Data.Items.Should().HaveCount(2);
        result.Data.Summary.BookingCount.Should().Be(2);
    }

    [Fact]
    public async Task The_totals_should_describe_the_whole_population_and_not_the_page()
    {
        SeedCustomer(1, "Santos");
        SeedPackage(10, "Wedding", 500m);

        for (var index = 0; index < 12; index++)
        {
            await SeedBooking(1, 10, Today.AddDays(index + 1), "Grand Ballroom");
        }

        var result = await _query.ExecuteAsync(new BookingRegisterFilterRequest
        {
            Page = 1,
            PageSize = 10
        });

        result.Succeeded.Should().BeTrue();
        result.Data!.Items.Should().HaveCount(10, "the page holds ten rows");
        result.Data.TotalCount.Should().Be(12, "the population holds twelve");
        result.Data.TotalPages.Should().Be(2);
        result.Data.Summary.BookingCount.Should().Be(12, "the totals describe the whole population");
        result.Data.Summary.NetContractedValue.Should().Be(12 * 50_000m);
        result.Data.Summary.TotalPax.Should().Be(12 * 100);
    }

    [Fact]
    public async Task The_collected_and_the_outstanding_should_sum_to_the_net_contracted_value()
    {
        SeedCustomer(1, "Santos");
        SeedPackage(10, "Wedding", 500m);

        var booking = await SeedBooking(1, 10, Today.AddDays(5), "Grand Ballroom");
        SeedPayment(booking, 20_000m);

        var result = await _query.ExecuteAsync(new BookingRegisterFilterRequest());

        var summary = result.Data!.Summary;
        summary.CollectedAmount.Should().Be(20_000m);
        summary.OutstandingAmount.Should().Be(30_000m);
        (summary.CollectedAmount + summary.OutstandingAmount).Should().Be(summary.NetContractedValue);
    }

    [Fact]
    public async Task A_soft_deleted_booking_should_be_excluded_from_the_population()
    {
        SeedCustomer(1, "Santos");
        SeedPackage(10, "Wedding", 500m);

        var booking = await SeedBooking(1, 10, Today.AddDays(5), "Grand Ballroom");
        booking.IsDeleted = true;

        var result = await _query.ExecuteAsync(new BookingRegisterFilterRequest());

        result.Data!.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task A_soft_deleted_payment_should_be_excluded_from_the_collected_amount()
    {
        SeedCustomer(1, "Santos");
        SeedPackage(10, "Wedding", 500m);

        var booking = await SeedBooking(1, 10, Today.AddDays(5), "Grand Ballroom");
        SeedPayment(booking, 20_000m, softDeleted: true);

        var result = await _query.ExecuteAsync(new BookingRegisterFilterRequest());

        result.Data!.Summary.CollectedAmount.Should().Be(0m);
        result.Data.Summary.OutstandingAmount.Should().Be(50_000m);
        result.Data.Items.Single().AmountPaid.Should().Be(0m);
    }

    [Fact]
    public async Task The_status_filter_should_narrow_the_population()
    {
        SeedCustomer(1, "Santos");
        SeedPackage(10, "Wedding", 500m);

        await SeedBooking(1, 10, Today.AddDays(5), "Grand Ballroom", status: BookingStatus.Confirmed);
        await SeedBooking(1, 10, Today.AddDays(6), "Grand Ballroom", status: BookingStatus.Pending);

        var result = await _query.ExecuteAsync(new BookingRegisterFilterRequest
        {
            Status = nameof(BookingStatus.Confirmed)
        });

        result.Data!.TotalCount.Should().Be(1);
        result.Data.Items.Single().Status.Should().Be(BookingStatus.Confirmed);
    }

    [Fact]
    public async Task An_unknown_status_name_should_be_rejected()
    {
        var result = await _query.ExecuteAsync(new BookingRegisterFilterRequest
        {
            Status = "NotAStatus"
        });

        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain("One or more of the booking statuses is not a known booking status.");
    }

    [Fact]
    public async Task An_unknown_payment_state_should_be_rejected()
    {
        var result = await _query.ExecuteAsync(new BookingRegisterFilterRequest
        {
            PaymentState = "FullyPaid"
        });

        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain("One or more of the payment states is not a known payment state.");
    }

    [Fact]
    public async Task An_invalid_page_size_should_be_rejected()
    {
        var result = await _query.ExecuteAsync(new BookingRegisterFilterRequest
        {
            PageSize = 17
        });

        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain("The page size must be 10, 25 or 50.");
    }

    [Fact]
    public async Task The_date_range_should_narrow_the_population()
    {
        SeedCustomer(1, "Santos");
        SeedPackage(10, "Wedding", 500m);

        await SeedBooking(1, 10, Today.AddDays(1), "Grand Ballroom");
        await SeedBooking(1, 10, Today.AddDays(10), "Grand Ballroom");
        await SeedBooking(1, 10, Today.AddDays(20), "Grand Ballroom");

        var result = await _query.ExecuteAsync(new BookingRegisterFilterRequest
        {
            FromDate = Today.AddDays(5),
            ToDate = Today.AddDays(15)
        });

        result.Data!.TotalCount.Should().Be(1);
    }

    [Fact]
    public async Task The_search_should_match_the_booking_number_the_occasion_the_venue_and_the_customer()
    {
        SeedCustomer(1, "Santos", "Sal's Corporation");
        SeedCustomer(2, "Dela Cruz");
        SeedPackage(10, "Wedding", 500m);

        await SeedBooking(1, 10, Today.AddDays(5), "Grand Ballroom");
        await SeedBooking(2, 10, Today.AddDays(6), "Garden Pavilion");

        var byCustomer = await _query.ExecuteAsync(new BookingRegisterFilterRequest { Search = "Santos" });
        byCustomer.Data!.TotalCount.Should().Be(1);

        var byCompany = await _query.ExecuteAsync(new BookingRegisterFilterRequest { Search = "Corporation" });
        byCompany.Data!.TotalCount.Should().Be(1);

        var byVenue = await _query.ExecuteAsync(new BookingRegisterFilterRequest { Search = "Garden" });
        byVenue.Data!.TotalCount.Should().Be(1);

        var byBookingNumber = await _query.ExecuteAsync(new BookingRegisterFilterRequest { Search = "BK-" });
        byBookingNumber.Data!.TotalCount.Should().Be(2);
    }

    [Fact]
    public async Task The_tier_facet_should_ignore_its_own_dimension()
    {
        SeedCustomer(1, "Santos");
        SeedPackage(10, "Wedding", 500m);
        SeedPackage(20, "Corporate", 400m);

        await SeedBooking(1, 10, Today.AddDays(5), "Grand Ballroom");
        await SeedBooking(1, 20, Today.AddDays(6), "Grand Ballroom");

        var result = await _query.ExecuteAsync(new BookingRegisterFilterRequest
        {
            PackageType = "Wedding"
        });

        // The page holds the wedding booking only…
        result.Data!.TotalCount.Should().Be(1);
        result.Data.Items.Single().PackageType.Should().Be("Wedding");

        // …but the tier facet counts both tiers, because the tier filter is removed from it.
        result.Data.QuickViewCounts.PackageTypes.Should().HaveCount(2);
        result.Data.QuickViewCounts.PackageTypes.Should().Contain(facet => facet.Key == "Wedding" && facet.Count == 1);
        result.Data.QuickViewCounts.PackageTypes.Should().Contain(facet => facet.Key == "Corporate" && facet.Count == 1);
    }

    [Fact]
    public async Task The_venue_facet_should_ignore_its_own_dimension()
    {
        SeedCustomer(1, "Santos");
        SeedPackage(10, "Wedding", 500m);

        await SeedBooking(1, 10, Today.AddDays(5), "Grand Ballroom");
        await SeedBooking(1, 10, Today.AddDays(6), "Garden Pavilion");

        var result = await _query.ExecuteAsync(new BookingRegisterFilterRequest
        {
            Venue = "Grand Ballroom"
        });

        result.Data!.TotalCount.Should().Be(1);
        result.Data.QuickViewCounts.Venues.Should().HaveCount(2);
    }

    [Fact]
    public async Task The_payment_state_facet_should_ignore_its_own_dimension()
    {
        SeedCustomer(1, "Santos");
        SeedPackage(10, "Wedding", 500m);

        var awaiting = await SeedBooking(1, 10, Today.AddDays(5), "Grand Ballroom");
        var partial = await SeedBooking(1, 10, Today.AddDays(6), "Grand Ballroom");
        var settled = await SeedBooking(1, 10, Today.AddDays(7), "Grand Ballroom");

        SeedPayment(awaiting, 0m);
        SeedPayment(partial, 20_000m);
        SeedPayment(settled, 50_000m);

        var result = await _query.ExecuteAsync(new BookingRegisterFilterRequest
        {
            PaymentState = BookingPaymentState.PartiallyPaid
        });

        result.Data!.TotalCount.Should().Be(1);
        result.Data.QuickViewCounts.PaymentStates.Should().HaveCount(3);
        result.Data.QuickViewCounts.PaymentStates
            .Should().Contain(facet => facet.Key == BookingPaymentState.AwaitingDeposit && facet.Count == 1);
        result.Data.QuickViewCounts.PaymentStates
            .Should().Contain(facet => facet.Key == BookingPaymentState.PartiallyPaid && facet.Count == 1);
        result.Data.QuickViewCounts.PaymentStates
            .Should().Contain(facet => facet.Key == BookingPaymentState.Settled && facet.Count == 1);
    }

    [Fact]
    public async Task The_overdue_filter_should_select_only_the_overdue_bookings()
    {
        SeedCustomer(1, "Santos");
        SeedPackage(10, "Wedding", 500m);

        await SeedBooking(1, 10, Today.AddDays(-5), "Grand Ballroom");
        await SeedBooking(1, 10, Today.AddDays(5), "Grand Ballroom");

        var result = await _query.ExecuteAsync(new BookingRegisterFilterRequest
        {
            Overdue = true
        });

        result.Data!.TotalCount.Should().Be(1);
        result.Data.Items.Single().IsOverdue.Should().BeTrue();
    }

    [Fact]
    public async Task An_explicit_overdue_false_should_select_only_the_bookings_that_are_not_overdue()
    {
        SeedCustomer(1, "Santos");
        SeedPackage(10, "Wedding", 500m);

        await SeedBooking(1, 10, Today.AddDays(-5), "Grand Ballroom");
        await SeedBooking(1, 10, Today.AddDays(5), "Grand Ballroom");

        var result = await _query.ExecuteAsync(new BookingRegisterFilterRequest
        {
            Overdue = false
        });

        result.Data!.TotalCount.Should().Be(1, "an explicit false narrows the population");
        result.Data.Items.Single().IsOverdue.Should().BeFalse();
        result.Data.Items.Single().EventDate.Should().Be(Today.AddDays(5));
    }

    [Fact]
    public async Task An_absent_overdue_filter_should_keep_both_the_overdue_and_the_not_overdue_bookings()
    {
        SeedCustomer(1, "Santos");
        SeedPackage(10, "Wedding", 500m);

        await SeedBooking(1, 10, Today.AddDays(-5), "Grand Ballroom");
        await SeedBooking(1, 10, Today.AddDays(5), "Grand Ballroom");

        var result = await _query.ExecuteAsync(new BookingRegisterFilterRequest());

        result.Data!.TotalCount.Should().Be(2);
        result.Data.Items.Should().Contain(item => item.IsOverdue);
        result.Data.Items.Should().Contain(item => !item.IsOverdue);
    }

    [Fact]
    public async Task Every_card_should_satisfy_net_amount_due_equals_paid_plus_outstanding()
    {
        SeedCustomer(1, "Santos");
        SeedPackage(10, "Wedding", 500m);

        var booking = await SeedBooking(1, 10, Today.AddDays(5), "Grand Ballroom");
        SeedPayment(booking, 12_345.67m);

        var result = await _query.ExecuteAsync(new BookingRegisterFilterRequest());

        var item = result.Data!.Items.Single();
        (item.NetAmountDue).Should().Be(item.AmountPaid + item.OutstandingBalance);
    }

    [Fact]
    public async Task The_export_should_cover_the_whole_filtered_population_and_never_the_page()
    {
        SeedCustomer(1, "Santos");
        SeedPackage(10, "Wedding", 500m);

        for (var index = 0; index < 12; index++)
        {
            await SeedBooking(1, 10, Today.AddDays(index + 1), "Grand Ballroom");
        }

        var result = await _query.ExecuteUnpagedAsync(new BookingRegisterFilterRequest
        {
            Page = 1,
            PageSize = 10
        });

        result.Succeeded.Should().BeTrue();
        result.Data!.Items.Should().HaveCount(12);
        result.Data.Totals.BookingCount.Should().Be(12);
    }
}
