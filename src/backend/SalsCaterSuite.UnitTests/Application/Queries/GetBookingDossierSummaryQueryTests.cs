namespace SalsCaterSuite.UnitTests.Application.Queries;

using FluentAssertions;
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
/// The register aggregate (REQ-BKG-05, KPI-33 .. KPI-39). Every figure is server-computed and
/// a <c>null</c> means <b>unknown</b>, never zero (BR-20).
/// </summary>
public class GetBookingDossierSummaryQueryTests
{
    private static readonly DateOnly Today = new(2026, 10, 22);

    private readonly FakeSalsCaterSuiteDataStore _store = new();
    private readonly FakeBookingRepository _bookings;
    private readonly FakeBookingPolicyProvider _policy = new();
    private readonly GetBookingRegisterSummaryQuery _query;

    public GetBookingDossierSummaryQueryTests()
    {
        _bookings = new FakeBookingRepository(_store);

        var population = new BookingRegisterPopulationService(_bookings, new BookingAmountDueCalculator());

        _query = new GetBookingRegisterSummaryQuery(
            population,
            _policy,
            new FakeDateTime(Today.ToDateTime(TimeOnly.MinValue)),
            new BookingRegisterFilterValidator());
    }

    private void SeedCustomer(int id, string lastName)
    {
        _store.Customers.Add(new Customer
        {
            CustomerId = id,
            FirstName = "Test",
            LastName = lastName
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
            venue: "Grand Ballroom",
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

    private void SeedPayment(Booking booking, decimal amount)
    {
        _store.Payments.Add(new Payment
        {
            PaymentNo = $"OR-{_store.Payments.Count + 1}",
            TransactionId = booking.TransactionId,
            AmountPaid = amount,
            Particular = "Deposit",
            DateOfPayment = Today.ToDateTime(new TimeOnly(9, 0)),
            PaymentMeans = PaymentMeans.Cash
        });
    }

    [Fact]
    public async Task The_pipeline_should_split_the_population_by_status()
    {
        SeedCustomer(1, "Santos");
        SeedPackage(10, "Wedding", 500m);

        await SeedBooking(1, 10, Today.AddDays(5), status: BookingStatus.Confirmed);
        await SeedBooking(1, 10, Today.AddDays(6), status: BookingStatus.Pending);
        await SeedBooking(1, 10, Today.AddDays(7), status: BookingStatus.Completed);
        await SeedBooking(1, 10, Today.AddDays(8), status: BookingStatus.NoShow);

        var result = await _query.ExecuteAsync(new BookingRegisterFilterRequest());

        var pipeline = result.Data!.Pipeline;
        pipeline.BookingCount.Should().Be(4);
        pipeline.ConfirmedCount.Should().Be(1);
        pipeline.TentativeCount.Should().Be(1);
        pipeline.CompletedCount.Should().Be(1);
        pipeline.OtherCount.Should().Be(1);
        pipeline.CommittedPax.Should().Be(400);
    }

    [Fact]
    public async Task The_contract_value_should_split_into_the_collected_and_the_outstanding()
    {
        SeedCustomer(1, "Santos");
        SeedPackage(10, "Wedding", 500m);

        var booking = await SeedBooking(1, 10, Today.AddDays(5));
        SeedPayment(booking, 20_000m);

        var result = await _query.ExecuteAsync(new BookingRegisterFilterRequest());

        var contractValue = result.Data!.ContractValue;
        contractValue.NetContractedValue.Should().Be(50_000m);
        contractValue.CollectedAmount.Should().Be(20_000m);
        contractValue.OutstandingAmount.Should().Be(30_000m);
        contractValue.CollectionRatioPercentage.Should().Be(40m);
    }

    [Fact]
    public async Task The_headcount_should_publish_the_total_the_mean_and_the_next_event()
    {
        SeedCustomer(1, "Santos");
        SeedPackage(10, "Wedding", 500m);

        await SeedBooking(1, 10, Today.AddDays(5), heads: 100);
        await SeedBooking(1, 10, Today.AddDays(9), heads: 200);

        var result = await _query.ExecuteAsync(new BookingRegisterFilterRequest());

        var headcount = result.Data!.Headcount;
        headcount.TotalPax.Should().Be(300);
        headcount.AveragePaxPerBooking.Should().Be(150m);
        headcount.NextEventDate.Should().Be(Today.AddDays(5));
        headcount.NextEventBookingNumber.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task The_payment_state_distribution_should_sum_to_the_booking_count()
    {
        SeedCustomer(1, "Santos");
        SeedPackage(10, "Wedding", 500m);

        var awaiting = await SeedBooking(1, 10, Today.AddDays(5));
        var partial = await SeedBooking(1, 10, Today.AddDays(6));
        var settled = await SeedBooking(1, 10, Today.AddDays(7));

        SeedPayment(awaiting, 0m);
        SeedPayment(partial, 20_000m);
        SeedPayment(settled, 50_000m);

        var result = await _query.ExecuteAsync(new BookingRegisterFilterRequest());

        var states = result.Data!.PaymentStates;
        states.Should().HaveCount(3);
        states.Sum(state => state.Count).Should().Be(result.Data.Pipeline.BookingCount);

        states.Single(state => state.Key == BookingPaymentState.AwaitingDeposit).Count.Should().Be(1);
        states.Single(state => state.Key == BookingPaymentState.PartiallyPaid).Count.Should().Be(1);
        states.Single(state => state.Key == BookingPaymentState.Settled).Count.Should().Be(1);

        // The amount of a state is its outstanding amount (KPI-37).
        states.Single(state => state.Key == BookingPaymentState.AwaitingDeposit).Amount.Should().Be(50_000m);
        states.Single(state => state.Key == BookingPaymentState.PartiallyPaid).Amount.Should().Be(30_000m);
        states.Single(state => state.Key == BookingPaymentState.Settled).Amount.Should().Be(0m);
    }

    [Fact]
    public async Task The_capacity_should_be_null_when_no_capacity_is_configured()
    {
        SeedCustomer(1, "Santos");
        SeedPackage(10, "Wedding", 500m);

        await SeedBooking(1, 10, Today.AddDays(3));

        var result = await _query.ExecuteAsync(new BookingRegisterFilterRequest());

        var capacity = result.Data!.Capacity;
        capacity.IsConfigured.Should().BeFalse();
        capacity.CapacityPax.Should().BeNull("an absent key means unknown, never zero");
        capacity.UtilisationPercentage.Should().BeNull();
        capacity.CommittedPax.Should().Be(100, "the load is still published");
    }

    [Fact]
    public async Task The_capacity_should_publish_the_utilisation_when_a_capacity_is_configured()
    {
        SeedCustomer(1, "Santos");
        SeedPackage(10, "Wedding", 500m);
        _policy.MaximumCapacityPax = 400;

        await SeedBooking(1, 10, Today.AddDays(3), heads: 100);

        var result = await _query.ExecuteAsync(new BookingRegisterFilterRequest());

        var capacity = result.Data!.Capacity;
        capacity.IsConfigured.Should().BeTrue();
        capacity.CapacityPax.Should().Be(400);
        capacity.CommittedPax.Should().Be(100);
        capacity.UtilisationPercentage.Should().Be(25m);
    }

    [Fact]
    public async Task The_receivables_should_count_the_overdue_bookings_measured_from_the_event_date()
    {
        SeedCustomer(1, "Santos");
        SeedPackage(10, "Wedding", 500m);

        await SeedBooking(1, 10, Today.AddDays(-5));
        await SeedBooking(1, 10, Today.AddDays(5));

        var result = await _query.ExecuteAsync(new BookingRegisterFilterRequest());

        var receivables = result.Data!.Receivables;
        receivables.OutstandingAmount.Should().Be(100_000m);
        receivables.BookingCount.Should().Be(2);
        receivables.OverdueCount.Should().Be(1);
        receivables.OverdueAmount.Should().Be(50_000m);
    }

    [Fact]
    public async Task The_leading_tier_should_be_the_tier_with_the_highest_net_contracted_value()
    {
        SeedCustomer(1, "Santos");
        SeedPackage(10, "Wedding", 500m);
        SeedPackage(20, "Corporate", 400m);

        await SeedBooking(1, 10, Today.AddDays(10), rate: 500m, heads: 100);   // 50,000
        await SeedBooking(1, 20, Today.AddDays(12), rate: 400m, heads: 100);   // 40,000

        var result = await _query.ExecuteAsync(new BookingRegisterFilterRequest());

        var leading = result.Data!.LeadingTier;
        leading.Should().NotBeNull();
        leading!.PackageType.Should().Be("Wedding");
        leading.NetContractedValue.Should().Be(50_000m);
        leading.SharePercentage.Should().Be(55.56m);
    }

    [Fact]
    public async Task The_leading_tier_should_be_null_for_an_empty_population()
    {
        var result = await _query.ExecuteAsync(new BookingRegisterFilterRequest());

        result.Data!.LeadingTier.Should().BeNull();
    }

    [Fact]
    public async Task The_leading_tier_change_should_be_null_when_the_preceding_window_is_empty()
    {
        SeedCustomer(1, "Santos");
        SeedPackage(10, "Wedding", 500m);

        await SeedBooking(1, 10, Today.AddDays(10));

        var result = await _query.ExecuteAsync(new BookingRegisterFilterRequest
        {
            FromDate = Today.AddDays(1),
            ToDate = Today.AddDays(30)
        });

        var leading = result.Data!.LeadingTier;
        leading.Should().NotBeNull();
        leading!.ChangePercentage.Should().BeNull("an absent preceding period is unknown, never zero");
        leading.PreviousNetContractedValue.Should().BeNull();
    }

    [Fact]
    public async Task The_leading_tier_change_should_compare_with_the_preceding_window()
    {
        SeedCustomer(1, "Santos");
        SeedPackage(10, "Wedding", 500m);

        // The preceding window: 2026-10-02 .. 2026-10-31 for a filter of 2026-11-01..2026-11-30.
        await SeedBooking(1, 10, new DateOnly(2026, 10, 15), rate: 500m, heads: 100);  // 50,000
        await SeedBooking(1, 10, new DateOnly(2026, 11, 10), rate: 500m, heads: 100);  // 50,000

        var result = await _query.ExecuteAsync(new BookingRegisterFilterRequest
        {
            FromDate = new DateOnly(2026, 11, 1),
            ToDate = new DateOnly(2026, 11, 30)
        });

        var leading = result.Data!.LeadingTier;
        leading.Should().NotBeNull();
        leading!.NetContractedValue.Should().Be(50_000m);
        leading.PreviousNetContractedValue.Should().Be(50_000m);
        leading.ChangePercentage.Should().Be(0m);
    }
}
