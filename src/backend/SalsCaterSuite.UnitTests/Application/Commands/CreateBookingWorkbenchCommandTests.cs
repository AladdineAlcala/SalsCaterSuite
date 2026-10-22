namespace SalsCaterSuite.UnitTests.Application.Commands;

using FluentAssertions;
using FluentValidation;
using Moq;
using SalsCaterSuite.Application.Common.Interfaces;
using SalsCaterSuite.Application.Features.Bookings;
using SalsCaterSuite.Application.Features.Bookings.Commands;
using SalsCaterSuite.Application.Features.Bookings.DTOs;
using SalsCaterSuite.Application.Features.Bookings.Services;
using SalsCaterSuite.Application.Features.Bookings.Validators;
using SalsCaterSuite.Application.Services;
using SalsCaterSuite.Domain.Entities;
using SalsCaterSuite.Domain.Enums;
using SalsCaterSuite.UnitTests.Common.Fakes;
using Xunit;

/// <summary>
/// The <b>extended</b> create path (REQ-BKG-05, BR-33, BR-25, BR-26, BR-02, BR-05).
/// <para>
/// The create path writes the frozen snapshot, so the stored <c>Booking.AmountDue</c> and the
/// shared calculator's <c>NetAmountDue</c> are equal to the cent, the surcharge is stored in
/// its own column, and an overpayment is refused before anything is written.
/// </para>
/// </summary>
public class CreateBookingWorkbenchCommandTests
{
    private readonly FakeSalsCaterSuiteDataStore _store = new();
    private readonly FakeCustomerRepository _customers;
    private readonly FakePackageRepository _packages;
    private readonly FakeAreaRepository _areas;
    private readonly FakeDiscountRepository _discounts;
    private readonly FakePaymentRepository _payments;
    private readonly FakeBookingRepository _bookings;
    private readonly FakeBookingPolicyProvider _policy = new();
    private readonly Mock<ICurrentUserService> _currentUser = new();
    private readonly CreateBookingCommand _command;

    public CreateBookingWorkbenchCommandTests()
    {
        _customers = new FakeCustomerRepository(_store);
        _packages = new FakePackageRepository(_store);
        _areas = new FakeAreaRepository(_store);
        _discounts = new FakeDiscountRepository(_store);
        _payments = new FakePaymentRepository(_store);
        _bookings = new FakeBookingRepository(_store);

        _currentUser.Setup(user => user.UserName).Returns("event_planner");

        _command = new CreateBookingCommand(
            _customers,
            _packages,
            _bookings,
            _currentUser.Object,
            new CreateBookingValidator(),
            new BookingCreateCapabilities
            {
                Areas = _areas,
                Discounts = _discounts,
                Payments = _payments,
                Policy = _policy,
                Pricing = new BookingPricingService(_policy)
            });
    }

    private void SeedCustomer(int id = 1)
    {
        _store.Customers.Add(new Customer { CustomerId = id, FirstName = "Maria", LastName = "Santos" });
    }

    private void SeedPackage(int id = 10, decimal rate = 820m, bool active = true)
    {
        _store.Packages.Add(new Package
        {
            PackageId = id,
            PackageType = "Premier Buffet",
            Description = "Premier Buffet Tier",
            AmountPax = rate,
            IsActive = active
        });
    }

    private void SeedArea(int id = 20, decimal? rate = 50m)
    {
        _store.Areas.Add(new Area { AreaId = id, AreaDetails = "Batangas City", AdditionalRate = rate });
    }

    private static CreateBookingRequestDto ValidRequest() => new()
    {
        CustomerId = 1,
        PackageId = 10,
        NoOfPerson = 120,
        Occasion = "Silver Wedding Anniversary",
        Venue = "Grand Ballroom",
        StartDate = new DateTime(2026, 11, 20, 17, 0, 0),
        EndDate = new DateTime(2026, 11, 20, 22, 0, 0),
        Confirm = true
    };

    [Fact]
    public async Task A_draft_should_be_created_in_the_pending_status()
    {
        SeedCustomer();
        SeedPackage();

        var request = ValidRequest();
        request.Confirm = false;

        var result = await _command.ExecuteAsync(request);

        result.Succeeded.Should().BeTrue();
        var booking = _bookings.Bookings.Single();
        booking.Status.Should().Be(BookingStatus.Pending, "a draft is a Pending booking (DEC-107)");
        _store.Payments.Should().BeEmpty("a draft never carries a payment");
    }

    [Fact]
    public async Task A_confirmed_booking_should_be_created_in_the_confirmed_status()
    {
        SeedCustomer();
        SeedPackage();

        var result = await _command.ExecuteAsync(ValidRequest());

        result.Succeeded.Should().BeTrue();
        _bookings.Bookings.Single().Status.Should().Be(BookingStatus.Confirmed);
    }

    [Fact]
    public async Task The_frozen_snapshot_should_agree_with_the_calculator_to_the_cent()
    {
        SeedCustomer();
        SeedPackage();
        SeedArea();

        var request = ValidRequest();
        request.ExtendedAreaId = 20;
        request.ApplyExtendedAmount = true;

        var result = await _command.ExecuteAsync(request);

        result.Succeeded.Should().BeTrue();

        var booking = _bookings.Bookings.Single();
        var breakdown = new BookingAmountDueCalculator().Calculate(booking, 0m);

        booking.AmountDue.Should().Be(breakdown.NetAmountDue);
        booking.AmountDue.Should().Be((820m + 50m) * 120);
    }

    [Fact]
    public async Task The_surcharge_should_be_stored_in_its_own_frozen_column()
    {
        SeedCustomer();
        SeedPackage();
        SeedArea();

        var request = ValidRequest();
        request.ExtendedAreaId = 20;
        request.ApplyExtendedAmount = true;

        await _command.ExecuteAsync(request);

        var booking = _bookings.Bookings.Single();
        booking.ExtendedAreaAmount.Should().Be(50m * 120, "the surcharge is stored, not inferred");
        booking.ApplyExtendedAmount.Should().BeTrue();
    }

    [Fact]
    public async Task The_discount_code_should_be_resolved_and_frozen()
    {
        SeedCustomer();
        SeedPackage();

        _store.Discounts.Add(new Discount
        {
            DiscountId = 1,
            DiscountCode = "TEN",
            Description = "Ten percent",
            Percentage = 10m,
            IsActive = true
        });

        var request = ValidRequest();
        request.DiscountCode = "TEN";

        var result = await _command.ExecuteAsync(request);

        result.Succeeded.Should().BeTrue();

        var booking = _bookings.Bookings.Single();
        booking.DiscountCode.Should().Be("TEN");
        booking.DiscountType.Should().Be(DiscountType.Percentage);
        booking.DiscountPercentage.Should().Be(10m);
        booking.DiscountAmount.Should().Be(820m * 120 * 0.10m);
        booking.AmountDue.Should().Be(820m * 120 * 0.90m);
    }

    [Fact]
    public async Task An_unknown_discount_code_should_be_refused_and_nothing_should_be_written()
    {
        SeedCustomer();
        SeedPackage();

        var request = ValidRequest();
        request.DiscountCode = "NOPE";

        var result = await _command.ExecuteAsync(request);

        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain("The discount code was not found.");
        _bookings.Bookings.Should().BeEmpty();
    }

    [Fact]
    public async Task An_ambiguous_discount_code_should_be_refused_and_nothing_should_be_written()
    {
        SeedCustomer();
        SeedPackage();

        _store.Discounts.Add(new Discount { DiscountId = 1, DiscountCode = "DUP", Description = "One", Percentage = 10m, IsActive = true });
        _store.Discounts.Add(new Discount { DiscountId = 2, DiscountCode = "DUP", Description = "Two", Percentage = 20m, IsActive = true });

        var request = ValidRequest();
        request.DiscountCode = "DUP";

        var result = await _command.ExecuteAsync(request);

        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain("The discount code is ambiguous.");
        _bookings.Bookings.Should().BeEmpty();
    }

    [Fact]
    public async Task The_deposit_should_be_stored_and_never_change_the_balance()
    {
        SeedCustomer();
        SeedPackage();

        var request = ValidRequest();
        request.DepositAmount = 25_000m;

        var result = await _command.ExecuteAsync(request);

        result.Succeeded.Should().BeTrue();

        var booking = _bookings.Bookings.Single();
        booking.DepositAmount.Should().Be(25_000m);
        booking.AmountDue.Should().Be(820m * 120, "the deposit is advisory");
    }

    [Fact]
    public async Task The_initial_payment_should_be_staged_with_the_booking()
    {
        SeedCustomer();
        SeedPackage();

        var request = ValidRequest();
        request.InitialPayment = new BookingInitialPaymentRequestDto
        {
            AmountPaid = 30_000m,
            PaymentMeans = PaymentMeans.Check,
            CheckNo = "CK-2026-88412",
            Particular = "Downpayment"
        };

        var result = await _command.ExecuteAsync(request);

        result.Succeeded.Should().BeTrue();

        var payment = _store.Payments.Single();
        payment.AmountPaid.Should().Be(30_000m);
        payment.PaymentMeans.Should().Be(PaymentMeans.Check);
        payment.CheckNo.Should().Be("CK-2026-88412");
        payment.PaymentNo.Should().MatchRegex("^OR-\\d{8}-\\d{4}$");

        // The booking's denormalized AmountPaid cache is refreshed by the domain method.
        _bookings.Bookings.Single().AmountPaid.Should().Be(30_000m);
    }

    [Fact]
    public async Task An_initial_payment_above_the_net_amount_due_should_be_refused_and_nothing_written()
    {
        SeedCustomer();
        SeedPackage();

        var request = ValidRequest();
        request.InitialPayment = new BookingInitialPaymentRequestDto
        {
            AmountPaid = 820m * 120 + 1m,
            PaymentMeans = PaymentMeans.Cash
        };

        var result = await _command.ExecuteAsync(request);

        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain("The initial payment cannot exceed the net amount due of the booking.");
        _bookings.Bookings.Should().BeEmpty("no booking row is created");
        _store.Payments.Should().BeEmpty("no payment row is created");
    }

    [Fact]
    public async Task An_initial_payment_exactly_equal_to_the_net_amount_due_should_be_accepted()
    {
        SeedCustomer();
        SeedPackage();

        var request = ValidRequest();
        request.InitialPayment = new BookingInitialPaymentRequestDto
        {
            AmountPaid = 820m * 120,
            PaymentMeans = PaymentMeans.Cash
        };

        var result = await _command.ExecuteAsync(request);

        result.Succeeded.Should().BeTrue();
        _store.Payments.Should().HaveCount(1);
    }

    [Fact]
    public async Task A_draft_carrying_an_initial_payment_should_be_rejected()
    {
        SeedCustomer();
        SeedPackage();

        var request = ValidRequest();
        request.Confirm = false;
        request.InitialPayment = new BookingInitialPaymentRequestDto
        {
            AmountPaid = 1_000m,
            PaymentMeans = PaymentMeans.Cash
        };

        var result = await _command.ExecuteAsync(request);

        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain("Only a confirmed booking may carry an initial payment.");
        _bookings.Bookings.Should().BeEmpty();
    }

    [Fact]
    public async Task The_extended_area_switch_without_a_municipality_should_be_rejected()
    {
        SeedCustomer();
        SeedPackage();

        var request = ValidRequest();
        request.ApplyExtendedAmount = true;

        var result = await _command.ExecuteAsync(request);

        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain("A municipality must be selected when the extended area amount is applied.");
    }

    [Fact]
    public async Task A_deactivated_tier_should_be_refused()
    {
        SeedCustomer();
        SeedPackage(active: false);

        var result = await _command.ExecuteAsync(ValidRequest());

        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain("Package with ID 10 is not an active package.");
        _bookings.Bookings.Should().BeEmpty();
    }

    [Fact]
    public async Task The_call_and_the_packout_times_should_be_stored()
    {
        SeedCustomer();
        SeedPackage();

        var request = ValidRequest();
        request.CallTime = new TimeSpan(9, 0, 0);
        request.PackoutTime = new TimeSpan(23, 0, 0);

        var result = await _command.ExecuteAsync(request);

        result.Succeeded.Should().BeTrue();

        var booking = _bookings.Bookings.Single();
        booking.CallTime.Should().Be(new TimeSpan(9, 0, 0));
        booking.PackoutTime.Should().Be(new TimeSpan(23, 0, 0));
    }

    /// <summary>
    /// BR-34 (ratified): the dispatch schedule is <b>ordered</b> against the programme — the
    /// call time must not be later than the programme's start and the pack-out time must not be
    /// earlier than its end. The rule is an ordering rule, not a "same day" rule: the columns
    /// are a time of day and the model forbids the overnight event outright.
    /// </summary>
    [Fact]
    public async Task A_call_time_later_than_the_programme_should_be_rejected()
    {
        SeedCustomer();
        SeedPackage();

        var request = ValidRequest();
        request.CallTime = new TimeSpan(18, 0, 0);   // the programme starts at 17:00

        var result = await _command.ExecuteAsync(request);

        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain("The call time must not be later than the programme's start time.");
        _bookings.Bookings.Should().BeEmpty("nothing is written");
    }

    [Fact]
    public async Task A_packout_time_earlier_than_the_programme_should_be_rejected()
    {
        SeedCustomer();
        SeedPackage();

        var request = ValidRequest();
        request.PackoutTime = new TimeSpan(21, 0, 0);   // the programme ends at 22:00

        var result = await _command.ExecuteAsync(request);

        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain("The pack-out time must not be earlier than the programme's end time.");
        _bookings.Bookings.Should().BeEmpty("nothing is written");
    }

    [Fact]
    public async Task A_call_time_at_the_programme_start_and_a_packout_at_its_end_should_be_accepted()
    {
        SeedCustomer();
        SeedPackage();

        var request = ValidRequest();
        request.CallTime = new TimeSpan(17, 0, 0);   // exactly the programme's start
        request.PackoutTime = new TimeSpan(22, 0, 0); // exactly the programme's end

        var result = await _command.ExecuteAsync(request);

        result.Succeeded.Should().BeTrue();
        _bookings.Bookings.Should().HaveCount(1);
    }
}
