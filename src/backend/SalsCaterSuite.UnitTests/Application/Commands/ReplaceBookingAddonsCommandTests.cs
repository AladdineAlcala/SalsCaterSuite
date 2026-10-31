namespace SalsCaterSuite.UnitTests.Application.Commands;

using FluentAssertions;
using SalsCaterSuite.Application.Features.Bookings;
using SalsCaterSuite.Application.Features.Bookings.Commands;
using SalsCaterSuite.Application.Features.Bookings.DTOs.Request;
using SalsCaterSuite.Application.Features.Bookings.DTOs.Response;
using SalsCaterSuite.Application.Features.Bookings.Models;
using SalsCaterSuite.Application.Features.Bookings.Services;
using SalsCaterSuite.Domain.Entities;
using SalsCaterSuite.Domain.Enums;
using SalsCaterSuite.UnitTests.Common.Fakes;
using Xunit;

/// <summary>
/// <c>PUT /api/bookings/{id}/addons</c> — the full-replace add-on write that <b>re-freezes</b> the
/// money (contract §16.4, DEC-134, DEC-152, BR-39, BR-05).
/// <para>
/// The write replaces the lines, recomputes the add-on total, re-freezes
/// <c>Subtotal</c>/<c>DiscountAmount</c>/<c>AmountDue</c>, <b>re-caps</b> (never recomputes) the
/// frozen discount, leaves the deposit alone, and refuses a write that would leave the collected
/// amount above the new net.
/// </para>
/// </summary>
public class ReplaceBookingAddonsCommandTests
{
    private static readonly DateOnly Today = new(2026, 10, 22);

    private readonly FakeSalsCaterSuiteDataStore _store = new();
    private readonly FakeBookingRepository _bookings;
    private readonly FakeAddonRepository _addons;
    private readonly FakeBookingPolicyProvider _policy = new();
    private readonly ReplaceBookingAddonsCommand _command;

    public ReplaceBookingAddonsCommandTests()
    {
        _bookings = new FakeBookingRepository(_store);
        _addons = new FakeAddonRepository(_store);

        var courseSets = new BookingMenuCourseSetBuilder(
            new FakePackageCourseRuleRepository(_store),
            new FakeMenuRepository(_store));

        _command = new ReplaceBookingAddonsCommand(
            _bookings,
            new BookingLineItemResolver(courseSets, _addons),
            new BookingAddonSetPresenter(_policy, new FakeDateTime(Today.ToDateTime(TimeOnly.MinValue))),
            _policy,
            new FakeDateTime(Today.ToDateTime(TimeOnly.MinValue)));

        SeedCatalogue();
    }

    // ─────────────────────────── the fixtures ───────────────────────────

    private void SeedCatalogue()
    {
        _store.AddonCategories.Add(new AddonCategory
        {
            AddonCategoryId = 1,
            AddonCategoryName = "Equipment"
        });

        _store.AddonCategories.Add(new AddonCategory
        {
            AddonCategoryId = 2,
            AddonCategoryName = "Service"
        });

        // A flat rental: the catalogue amount IS the whole line.
        _store.AddonDetails.Add(new AddonDetail
        {
            AddonId = 10,
            AddonCategoryId = 1,
            Description = "Chafing dish rental",
            UnitPrice = 1_500m,
            Unit = "set",
            PricingBasis = AddonPricingBasis.Flat,
            IsActive = true
        });

        // A per-guest item: the catalogue amount is multiplied by the headcount.
        _store.AddonDetails.Add(new AddonDetail
        {
            AddonId = 20,
            AddonCategoryId = 2,
            Description = "Waiter service",
            UnitPrice = 50m,
            Unit = "head",
            PricingBasis = AddonPricingBasis.PerGuest,
            IsActive = true
        });

        // No stated basis: the server treats it as Flat (DEC-158).
        _store.AddonDetails.Add(new AddonDetail
        {
            AddonId = 30,
            Description = "Carving station",
            UnitPrice = 2_000m,
            Unit = "station",
            PricingBasis = null,
            IsActive = true
        });

        // An inactive row: it may not be booked.
        _store.AddonDetails.Add(new AddonDetail
        {
            AddonId = 40,
            Description = "Retired ice sculpture",
            UnitPrice = 5_000m,
            PricingBasis = AddonPricingBasis.Flat,
            IsActive = false
        });
    }

    /// <summary>
    /// Seeds the booking the write acts on. 100 heads at 500.00 is a gross of 50,000.00; the
    /// caller decides the frozen discount, the deposit and the collected amount.
    /// </summary>
    private Booking SeedBooking(
        int heads = 100,
        decimal pricePerHead = 500m,
        decimal discountAmount = 0m,
        decimal depositAmount = 0m,
        decimal collected = 0m,
        int eventDateOffsetDays = 60,
        BookingStatus status = BookingStatus.Confirmed)
    {
        var eventDate = Today.AddDays(eventDateOffsetDays);
        var start = eventDate.ToDateTime(new TimeOnly(17, 0));

        var booking = Booking.Create(
            customerId: 1,
            noOfPerson: heads,
            bookType: BookingType.Inside,
            typeOfService: ServiceType.Catering,
            eventDate: eventDate,
            startDate: start,
            endDate: start.AddHours(5),
            occasion: "Integration Event",
            venue: "Grand Ballroom",
            packageId: 10,
            packageName: "2026 Premier Package B",
            pricePerHead: pricePerHead);

        booking.RecalculateFinancials(
            extendedAreaAmount: 0m,
            addonsAmount: 0m,
            discountAmount: discountAmount,
            taxAmount: 0m);

        if (depositAmount > 0m)
        {
            booking.SetDepositAmount(depositAmount);
        }

        if (status != BookingStatus.Pending)
        {
            booking.Confirm();
        }

        _store.Bookings.Add(booking);
        _store.AssignTransactionId(booking);
        booking.AssignBookingNumber();

        if (collected > 0m)
        {
            _store.Payments.Add(new Payment
            {
                PaymentNo = "OR-20261022-0001",
                TransactionId = booking.TransactionId,
                DateOfPayment = DateTime.UtcNow,
                Particular = "Downpayment",
                PaymentMeans = PaymentMeans.Cash,
                AmountPaid = collected,
                CreatedByUser = "unit-test"
            });

            booking.RecordPayment(collected);
        }

        return booking;
    }

    private static BookingAddonRequest Line(int addonId, decimal? amount = null) =>
        new() { AddonId = addonId, Amount = amount };

    private static BookingAddonsRequest Request(params BookingAddonRequest[] lines) =>
        new() { Addons = lines.ToList() };

    // ─────────────────────────── the happy path ───────────────────────────

    [Fact]
    public async Task The_write_should_freeze_the_lines_and_recompute_the_add_on_total()
    {
        var booking = SeedBooking();

        var result = await _command.ExecuteAsync(booking.TransactionId, Request(Line(10), Line(20), Line(30)));

        result.Succeeded.Should().BeTrue();

        // 1,500 flat + (50 x 100 heads) per guest + 2,000 flat (no stated basis).
        result.Data!.AddonsTotal.Should().Be(8_500m);
        result.Data.Addons.Should().HaveCount(3);
        result.Data.Addons.Single(line => line.AddonId == 20).Amount.Should().Be(5_000m);
        result.Data.Addons.Single(line => line.AddonId == 30).PricingBasis.Should().Be(AddonPricingBasis.Flat);
        result.Data.Addons.Single(line => line.AddonId == 10).AddonCategoryName.Should().Be("Equipment");
    }

    [Fact]
    public async Task The_write_should_re_freeze_the_subtotal_and_the_amount_due()
    {
        var booking = SeedBooking();

        await _command.ExecuteAsync(booking.TransactionId, Request(Line(10)));

        booking.AddonsTotal.Should().Be(1_500m);
        booking.Subtotal.Should().Be(51_500m, "the add-on total joins the gross (BR-01)");
        booking.AmountDue.Should().Be(51_500m);
        booking.ExtendedAreaAmount.Should().Be(0m, "the write never touches the surcharge");
    }

    [Fact]
    public async Task An_explicit_line_total_should_win_over_the_catalogue_default()
    {
        var booking = SeedBooking();

        var result = await _command.ExecuteAsync(
            booking.TransactionId,
            Request(Line(20, amount: 4_250m)));

        result.Data!.Addons.Single().Amount.Should().Be(4_250m);
        booking.AddonsTotal.Should().Be(4_250m);
        booking.AmountDue.Should().Be(54_250m);
    }

    // ─────────────────────────── the replace semantics ───────────────────────────

    [Fact]
    public async Task Omitting_a_line_should_soft_delete_it_and_reduce_the_total()
    {
        var booking = SeedBooking();
        await _command.ExecuteAsync(booking.TransactionId, Request(Line(10), Line(20)));

        var result = await _command.ExecuteAsync(booking.TransactionId, Request(Line(10)));

        result.Succeeded.Should().BeTrue();
        result.Data!.Addons.Should().ContainSingle().Which.AddonId.Should().Be(10);
        result.Data.AddonsTotal.Should().Be(1_500m);

        booking.AddOns.Should().HaveCount(2, "the removed row is kept, soft-deleted (DEC-085)");
        booking.AddOns.Single(line => line.AddonId == 20).IsDeleted.Should().BeTrue();
        booking.Subtotal.Should().Be(51_500m);
    }

    [Fact]
    public async Task Re_adding_a_line_should_resurrect_its_row_rather_than_insert_a_second_one()
    {
        var booking = SeedBooking();
        await _command.ExecuteAsync(booking.TransactionId, Request(Line(10)));
        await _command.ExecuteAsync(booking.TransactionId, Request());

        await _command.ExecuteAsync(booking.TransactionId, Request(Line(10)));

        booking.AddOns.Should().ContainSingle("the filtered unique index forbids a second live row");
        booking.AddOns.Single().IsDeleted.Should().BeFalse();
        booking.AddonsTotal.Should().Be(1_500m);
    }

    [Fact]
    public async Task An_empty_array_should_clear_every_line_and_return_to_the_base_gross()
    {
        var booking = SeedBooking();
        await _command.ExecuteAsync(booking.TransactionId, Request(Line(10), Line(20)));

        var result = await _command.ExecuteAsync(booking.TransactionId, Request());

        result.Succeeded.Should().BeTrue();
        result.Data!.Addons.Should().BeEmpty();
        result.Data.AddonsTotal.Should().Be(0m);
        booking.Subtotal.Should().Be(50_000m);
        booking.AmountDue.Should().Be(50_000m);
    }

    /// <summary>
    /// DEC-085: an absent <c>addons</c> member is "nothing was submitted", never "clear
    /// everything". Only an explicitly empty array clears.
    /// </summary>
    [Fact]
    public async Task An_absent_member_should_be_rejected_and_nothing_should_be_written()
    {
        var booking = SeedBooking();

        var result = await _command.ExecuteAsync(booking.TransactionId, new BookingAddonsRequest());

        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain(
            "The 'addons' member is required. Send an empty array to clear the lines.");
        _bookings.SaveCount.Should().Be(0);
    }

    // ─────────────────────────── the re-freeze rules ───────────────────────────

    /// <summary>
    /// DEC-152: the frozen discount is <b>re-capped</b> at the new gross, never recomputed — the
    /// band depends on the headcount, and this write does not change it.
    /// </summary>
    [Fact]
    public async Task The_frozen_discount_should_be_kept_and_only_capped_at_the_new_gross()
    {
        var booking = SeedBooking(discountAmount: 2_000m);

        await _command.ExecuteAsync(booking.TransactionId, Request(Line(10)));

        booking.DiscountAmount.Should().Be(2_000m, "a larger gross does not re-earn the band");
        booking.AmountDue.Should().Be(49_500m);
    }

    [Fact]
    public async Task A_discount_larger_than_the_new_gross_should_be_capped_at_the_gross()
    {
        var booking = SeedBooking(heads: 100, pricePerHead: 20m);
        booking.RecalculateFinancials(0m, 0m, 2_000m, 0m);

        await _command.ExecuteAsync(booking.TransactionId, Request());

        booking.Subtotal.Should().Be(2_000m);
        booking.DiscountAmount.Should().Be(2_000m);
        booking.AmountDue.Should().Be(0m);
    }

    [Fact]
    public async Task The_deposit_should_never_be_touched_by_the_write()
    {
        var booking = SeedBooking(depositAmount: 20_000m);

        await _command.ExecuteAsync(booking.TransactionId, Request(Line(10)));

        booking.DepositAmount.Should().Be(20_000m);
    }

    /// <summary>
    /// BR-05: a payment may never exceed the net amount due. The operator settles the credit
    /// first, so the write is refused and <b>nothing</b> is written.
    /// </summary>
    [Fact]
    public async Task A_write_that_would_leave_the_collected_amount_above_the_new_net_should_be_refused()
    {
        // 10 heads x 100.00 = 1,000.00 gross; the per-guest 50.00 line takes it to 1,500.00.
        var booking = SeedBooking(heads: 10, pricePerHead: 100m);
        await _command.ExecuteAsync(booking.TransactionId, Request(Line(20)));
        booking.AmountDue.Should().Be(1_500m);

        // The client pays 1,400.00 — legal against 1,500.00.
        _store.Payments.Add(new Payment
        {
            PaymentNo = "OR-20261022-0001",
            TransactionId = booking.TransactionId,
            DateOfPayment = DateTime.UtcNow,
            Particular = "Downpayment",
            PaymentMeans = PaymentMeans.Cash,
            AmountPaid = 1_400m,
            CreatedByUser = "unit-test"
        });
        booking.RecordPayment(1_400m);

        var beforeSave = _bookings.SaveCount;

        // Clearing the line takes the net back to 1,000.00, below the 1,400.00 collected.
        var result = await _command.ExecuteAsync(booking.TransactionId, Request());

        result.Succeeded.Should().BeFalse();
        result.Errors.Single().Should().Be(
            "The add-on lines would leave the net amount due at 1000.00, below the 1400.00 " +
            "already collected. Settle the credit before reducing the lines.");

        _bookings.SaveCount.Should().Be(beforeSave, "nothing is written until the credit is settled");
        booking.AddonsTotal.Should().Be(500m, "the lines are untouched");
        booking.AmountDue.Should().Be(1_500m);
        booking.AddOns.Single().IsDeleted.Should().BeFalse();
    }

    [Fact]
    public async Task A_cancelled_booking_should_refuse_the_write()
    {
        var booking = SeedBooking();
        booking.Cancel();

        var result = await _command.ExecuteAsync(booking.TransactionId, Request(Line(10)));

        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain("A cancelled booking's add-on lines cannot be changed.");
    }

    [Fact]
    public async Task A_completed_booking_should_refuse_the_write()
    {
        var booking = SeedBooking();
        booking.MarkCompleted();

        var result = await _command.ExecuteAsync(booking.TransactionId, Request(Line(10)));

        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain("A completed booking's add-on lines cannot be changed.");
    }

    [Fact]
    public async Task A_locked_window_should_refuse_the_write_and_name_the_lock_date()
    {
        var booking = SeedBooking(eventDateOffsetDays: 3);
        _policy.MenuSelectionLockDaysBeforeEvent = 7;

        var result = await _command.ExecuteAsync(booking.TransactionId, Request(Line(10)));

        result.Succeeded.Should().BeFalse();
        result.Errors.Single().Should().StartWith("The add-on lines cannot be changed: the menu-change window closed on ");
        result.Errors.Single().Should().Contain(booking.EventDate.AddDays(-7).ToString("yyyy-MM-dd"));
    }

    [Fact]
    public async Task An_unknown_booking_should_be_reported_as_not_found()
    {
        var result = await _command.ExecuteAsync(9_999, Request(Line(10)));

        result.Should().BeOfType<BookingNotFoundResult<BookingAddonSetResponse>>();
        result.Errors.Should().Contain("Booking not found.");
    }

    // ─────────────────────────── the line validation ───────────────────────────

    [Fact]
    public async Task An_unknown_add_on_should_be_refused()
    {
        var booking = SeedBooking();

        var result = await _command.ExecuteAsync(booking.TransactionId, Request(Line(999)));

        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain("The add-on with ID 999 was not found.");
    }

    [Fact]
    public async Task An_inactive_add_on_should_be_refused()
    {
        var booking = SeedBooking();

        var result = await _command.ExecuteAsync(booking.TransactionId, Request(Line(40)));

        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain(
            "The add-on 'Retired ice sculpture' is not active in the catalogue.");
    }

    [Fact]
    public async Task A_duplicate_add_on_should_be_refused()
    {
        var booking = SeedBooking();

        var result = await _command.ExecuteAsync(
            booking.TransactionId,
            Request(Line(10), Line(10, amount: 500m)));

        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain("The add-on with ID 10 is listed more than once.");
    }

    [Fact]
    public async Task A_negative_line_total_should_be_refused()
    {
        var booking = SeedBooking();

        var result = await _command.ExecuteAsync(
            booking.TransactionId,
            Request(Line(10, amount: -1m)));

        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain(
            "The line total of the add-on 'Chafing dish rental' must be zero or greater.");
    }

    [Fact]
    public async Task A_zero_line_total_should_be_accepted()
    {
        var booking = SeedBooking();

        var result = await _command.ExecuteAsync(
            booking.TransactionId,
            Request(Line(10, amount: 0m)));

        result.Succeeded.Should().BeTrue();
        result.Data!.Addons.Single().Amount.Should().Be(0m);
        booking.AmountDue.Should().Be(50_000m);
    }

    // ─────────────────────────── the catalogue view ───────────────────────────

    /// <summary>
    /// The write's response is built by the same presenter the read uses, so a catalogue price
    /// change is published <b>beside</b> the frozen line rather than replacing it (BR-04).
    /// </summary>
    [Fact]
    public async Task The_response_should_publish_the_frozen_line_beside_the_live_catalogue()
    {
        var booking = SeedBooking();
        await _command.ExecuteAsync(booking.TransactionId, Request(Line(10)));

        _store.AddonDetails.Single(addon => addon.AddonId == 10).Description = "Chafing dish rental (new)";
        _store.AddonDetails.Single(addon => addon.AddonId == 10).UnitPrice = 2_000m;

        var result = await _command.ExecuteAsync(booking.TransactionId, Request(Line(10, amount: 1_500m)));

        var line = result.Data!.Addons.Single();
        line.Amount.Should().Be(1_500m, "the request stated the line total");
        line.Description.Should().Be("Chafing dish rental (new)", "the write re-freezes from the catalogue");
        line.CatalogueAmount.Should().Be(2_000m, "the catalogue's current amount is published alongside");
        booking.AmountDue.Should().Be(51_500m);
    }
}
