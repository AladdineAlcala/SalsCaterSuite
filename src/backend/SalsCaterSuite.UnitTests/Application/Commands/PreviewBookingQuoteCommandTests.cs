namespace SalsCaterSuite.UnitTests.Application.Commands;

using FluentAssertions;
using SalsCaterSuite.Application.Features.Bookings.Commands;
using SalsCaterSuite.Application.Features.Bookings.DTOs;
using SalsCaterSuite.Application.Features.Bookings.Services;
using SalsCaterSuite.Application.Features.Bookings.Validators;
using SalsCaterSuite.Application.Features.Packages.Services;
using SalsCaterSuite.Domain.Entities;
using SalsCaterSuite.Domain.Enums;
using SalsCaterSuite.UnitTests.Common.Fakes;
using Xunit;

/// <summary>
/// The workbench quote (REQ-BKG-01, BR-31, BR-25, BR-29, BR-30). The quote prices a
/// <b>proposed</b> booking and persists nothing.
/// </summary>
public class PreviewBookingQuoteCommandTests
{
    private static readonly DateOnly Today = new(2026, 10, 22);

    private readonly FakeSalsCaterSuiteDataStore _store = new();
    private readonly FakeCustomerRepository _customers;
    private readonly FakePackageRepository _packages;
    private readonly FakeAreaRepository _areas;
    private readonly FakeDiscountRepository _discounts;
    private readonly FakePackageAreaCoverageRepository _coverages;
    private readonly FakeBookingRepository _bookings;
    private readonly FakeMenuRepository _menus;
    private readonly FakePackageCourseRuleRepository _rules;
    private readonly FakeBookingPolicyProvider _policy = new();
    private readonly PreviewBookingQuoteCommand _command;

    public PreviewBookingQuoteCommandTests()
    {
        _customers = new FakeCustomerRepository(_store);
        _packages = new FakePackageRepository(_store);
        _areas = new FakeAreaRepository(_store);
        _discounts = new FakeDiscountRepository(_store);
        _coverages = new FakePackageAreaCoverageRepository(_store);
        _bookings = new FakeBookingRepository(_store);
        _menus = new FakeMenuRepository(_store);
        _rules = new FakePackageCourseRuleRepository(_store);

        var pricing = new BookingPricingService(_policy);
        var estimates = new BookingTierEstimateService(_packages, _rules, _menus);
        var courseSets = new BookingMenuCourseSetBuilder(_rules, _menus);
        var addons = new FakeAddonRepository(_store);
        var lineItems = new BookingLineItemResolver(courseSets, addons);

        _command = new PreviewBookingQuoteCommand(
            new BookingQuoteValidator(),
            _customers,
            _packages,
            _areas,
            _discounts,
            _coverages,
            _bookings,
            pricing,
            lineItems,
            courseSets,
            estimates,
            _policy,
            new FakeDateTime(Today.ToDateTime(TimeOnly.MinValue)));
    }

    private void SeedCustomer(int id = 1, bool softDeleted = false)
    {
        _store.Customers.Add(new Customer
        {
            CustomerId = id,
            FirstName = "Maria",
            LastName = "Santos",
            IsDeleted = softDeleted
        });
    }

    private void SeedPackage(int id = 10, decimal rate = 820m, bool active = true, int? minPax = null)
    {
        _store.Packages.Add(new Package
        {
            PackageId = id,
            // BR-40 (DEC-141): the out-of-town surcharge is limited to the Regular and
            // Premier tier TYPES verbatim, so the fixture is typed "Regular" — its description
            // is the development catalogue's own, which mentions "Premier" and must not count.
            PackageType = "Regular",
            Description = "2026 Premier Package B",
            AmountPax = rate,
            MinPax = minPax,
            IsActive = active
        });
    }

    private void SeedArea(int id = 20, decimal? rate = 50m)
    {
        _store.Areas.Add(new Area { AreaId = id, AreaDetails = "Batangas City", AdditionalRate = rate });
    }

    private static BookingQuoteRequestDto ValidRequest() => new()
    {
        CustomerId = 1,
        PackageId = 10,
        NoOfPerson = 120,
        Occasion = "Silver Wedding Anniversary",
        Venue = "Grand Ballroom",
        EventDate = Today.AddDays(30),
        StartTime = new TimeSpan(17, 0, 0),
        EndTime = new TimeSpan(22, 0, 0)
    };

    [Fact]
    public async Task The_quote_should_publish_the_base_the_surcharge_the_gross_and_the_net()
    {
        SeedCustomer();
        SeedPackage();
        SeedArea();

        var request = ValidRequest();
        request.ExtendedAreaId = 20;
        request.ApplyExtendedAmount = true;

        var result = await _command.ExecuteAsync(request);

        result.Succeeded.Should().BeTrue();
        var quote = result.Data!;

        quote.BaseAmount.Should().Be(820m * 120);
        quote.ExtendedAreaAmount.Should().Be(50m * 120, "the surcharge is the municipality's rate times the pax");
        quote.ExtendedAreaRate.Should().Be(50m);
        quote.GrossAmount.Should().Be((820m + 50m) * 120);

        // BR-38 (DEC-136): 120 heads falls in the 100-999 band, so the automatic discount
        // takes 20.00 per head off the gross and is the only discount on the quote.
        quote.DiscountSource.Should().Be(DiscountSource.HeadcountBand);
        quote.DiscountBandLabel.Should().Be(HeadcountBandDiscount.Band100To999Label);
        quote.DiscountAmount.Should().Be(2_400m);
        quote.NetAmountDue.Should().Be(quote.GrossAmount - 2_400m);
    }

    [Fact]
    public async Task The_surcharge_should_not_be_charged_when_the_switch_is_off()
    {
        SeedCustomer();
        SeedPackage();
        SeedArea();

        var request = ValidRequest();
        request.ExtendedAreaId = 20;
        request.ApplyExtendedAmount = false;

        var result = await _command.ExecuteAsync(request);

        result.Data!.ExtendedAreaAmount.Should().Be(0m);
        result.Data.ExtendedAreaRate.Should().Be(50m, "the published rate stays the municipality's");
    }

    [Fact]
    public async Task A_municipality_without_a_stated_rate_should_charge_nothing_and_publish_null()
    {
        SeedCustomer();
        SeedPackage();
        SeedArea(rate: null);

        var request = ValidRequest();
        request.ExtendedAreaId = 20;
        request.ApplyExtendedAmount = true;

        var result = await _command.ExecuteAsync(request);

        result.Data!.ExtendedAreaAmount.Should().Be(0m, "an absent addition is nothing");
        result.Data.ExtendedAreaRate.Should().BeNull("the measurement is unknown");
    }

    [Fact]
    public async Task A_fixed_discount_should_take_precedence_over_a_percentage()
    {
        SeedCustomer();
        SeedPackage();

        _store.Discounts.Add(new Discount
        {
            DiscountId = 1,
            DiscountCode = "EARLYBIRD",
            Description = "Early bird",
            Percentage = 10m,
            FixedAmount = 5_000m,
            IsActive = true
        });

        var request = ValidRequest();
        request.DiscountCode = "EARLYBIRD";

        var result = await _command.ExecuteAsync(request);

        result.Succeeded.Should().BeTrue();
        result.Data!.DiscountType.Should().Be(DiscountType.FixedAmount);
        result.Data.DiscountAmount.Should().Be(5_000m);
        result.Data.DiscountCode.Should().Be("EARLYBIRD");
    }

    [Fact]
    public async Task A_percentage_discount_should_be_charged_on_the_gross_amount()
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

        result.Data!.DiscountType.Should().Be(DiscountType.Percentage);
        result.Data.DiscountAmount.Should().Be(820m * 120 * 0.10m);
        result.Data.NetAmountDue.Should().Be(820m * 120 * 0.90m);
    }

    [Fact]
    public async Task The_discount_should_be_capped_at_the_gross_amount()
    {
        SeedCustomer();
        SeedPackage();

        _store.Discounts.Add(new Discount
        {
            DiscountId = 1,
            DiscountCode = "HUGE",
            Description = "A hundred percent",
            Percentage = 100m,
            IsActive = true
        });

        var request = ValidRequest();
        request.DiscountCode = "HUGE";

        var result = await _command.ExecuteAsync(request);

        result.Data!.DiscountAmount.Should().Be(820m * 120);
        result.Data.NetAmountDue.Should().Be(0m);
    }

    [Fact]
    public async Task An_unknown_discount_code_should_be_rejected()
    {
        SeedCustomer();
        SeedPackage();

        var request = ValidRequest();
        request.DiscountCode = "NOPE";

        var result = await _command.ExecuteAsync(request);

        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain("The discount code was not found.");
    }

    [Fact]
    public async Task An_ambiguous_discount_code_should_be_rejected()
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
    }

    [Fact]
    public async Task The_required_deposit_should_be_null_when_no_percentage_is_configured()
    {
        SeedCustomer();
        SeedPackage();

        var result = await _command.ExecuteAsync(ValidRequest());

        result.Data!.RequiredDepositPercentage.Should().BeNull("the key is absent by default");
        result.Data.RequiredDepositAmount.Should().BeNull();
        result.Data.DepositAmount.Should().Be(0m);
    }

    [Fact]
    public async Task The_required_deposit_should_be_computed_when_a_percentage_is_configured()
    {
        SeedCustomer();
        SeedPackage();
        _policy.RequiredDepositPercentage = 30m;

        var result = await _command.ExecuteAsync(ValidRequest());

        result.Data!.RequiredDepositPercentage.Should().Be(30m);
        result.Data.RequiredDepositAmount.Should().Be(
            (820m * 120 - 2_400m) * 0.30m,
            "the required deposit is a share of the net, which already carries the band (BR-30, BR-38)");
    }

    [Fact]
    public async Task The_capacity_should_be_null_when_no_capacity_is_configured()
    {
        SeedCustomer();
        SeedPackage();

        var result = await _command.ExecuteAsync(ValidRequest());

        result.Data!.Capacity.CapacityPax.Should().BeNull();
        result.Data.Capacity.IsConfigured.Should().BeFalse();
        result.Data.Capacity.UtilisationPercentage.Should().BeNull();
    }

    [Fact]
    public async Task The_outstanding_balance_should_be_the_net_less_the_stated_initial_payment()
    {
        SeedCustomer();
        SeedPackage();

        var request = ValidRequest();
        request.InitialPaymentAmount = 50_000m;

        var result = await _command.ExecuteAsync(request);

        result.Data!.OutstandingBalance.Should().Be(
            820m * 120 - 2_400m - 50_000m,
            "the outstanding balance is the net (band included) less the stated initial payment");
        result.Data.InitialPaymentAmount.Should().Be(50_000m);
    }

    [Fact]
    public async Task The_five_named_checks_should_always_be_present_and_only_the_tier_check_blocks()
    {
        SeedCustomer();
        SeedPackage();

        var result = await _command.ExecuteAsync(ValidRequest());

        var checks = result.Data!.Checks;
        checks.Should().HaveCount(5);
        checks.Select(check => check.Key).Should().BeEquivalentTo(new[]
        {
            "ClientValidated", "TierActive", "CoursesSpecified", "MunicipalityCovered", "DepositMatched"
        });
        checks.Should().ContainSingle(check => check.IsBlocking);
        checks.Single(check => check.Key == "TierActive").IsBlocking.Should().BeTrue();
    }

    [Fact]
    public async Task A_deactivated_tier_should_fail_the_blocking_check()
    {
        SeedCustomer();
        SeedPackage(active: false);

        var result = await _command.ExecuteAsync(ValidRequest());

        var tierCheck = result.Data!.Checks.Single(check => check.Key == "TierActive");
        tierCheck.IsSatisfied.Should().BeFalse();
        tierCheck.IsBlocking.Should().BeTrue();
        result.Data.IsTierActive.Should().BeFalse();
    }

    [Fact]
    public async Task The_coverage_verdict_should_be_null_when_the_tier_has_no_coverage_set()
    {
        SeedCustomer();
        SeedPackage();
        SeedArea();

        var request = ValidRequest();
        request.ExtendedAreaId = 20;

        var result = await _command.ExecuteAsync(request);

        result.Data!.Coverage.IsCovered.Should().BeNull("unknown, never a breach");
        result.Data.Coverage.CoveredAreaCount.Should().Be(0);
    }

    [Fact]
    public async Task The_coverage_verdict_should_be_true_when_the_municipality_is_covered()
    {
        SeedCustomer();
        SeedPackage();
        SeedArea();

        _store.PackageAreaCoverages.Add(new PackageAreaCoverage
        {
            PackageAreaCoverageId = 1,
            PackageId = 10,
            AreaId = 20,
            IsActive = true
        });

        var request = ValidRequest();
        request.ExtendedAreaId = 20;

        var result = await _command.ExecuteAsync(request);

        result.Data!.Coverage.IsCovered.Should().BeTrue();
        result.Data.Coverage.CoveredAreaCount.Should().Be(1);
    }

    [Fact]
    public async Task The_headcount_floor_should_be_an_advisory_note_and_never_block()
    {
        SeedCustomer();
        SeedPackage(minPax: 150);

        var result = await _command.ExecuteAsync(ValidRequest());

        result.Data!.HeadcountFloorNote.Should().NotBeNull();
        result.Data.Checks.Should().NotContain(check => check.Key == "MinPax");
    }

    [Fact]
    public async Task The_quote_should_persist_nothing()
    {
        SeedCustomer();
        SeedPackage();
        SeedArea();

        var request = ValidRequest();
        request.ExtendedAreaId = 20;
        request.ApplyExtendedAmount = true;
        request.DiscountCode = "TEN";

        _store.Discounts.Add(new Discount { DiscountId = 1, DiscountCode = "TEN", Description = "Ten", Percentage = 10m, IsActive = true });

        var result = await _command.ExecuteAsync(request);

        result.Succeeded.Should().BeTrue();
        _bookings.Bookings.Should().BeEmpty("the quote is a what-if");
        _store.Payments.Should().BeEmpty();
    }
}
