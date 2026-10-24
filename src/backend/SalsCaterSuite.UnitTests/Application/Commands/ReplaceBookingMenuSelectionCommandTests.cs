namespace SalsCaterSuite.UnitTests.Application.Commands;

using FluentAssertions;
using SalsCaterSuite.Application.Features.Bookings;
using SalsCaterSuite.Application.Features.Bookings.Commands;
using SalsCaterSuite.Application.Features.Bookings.DTOs;
using SalsCaterSuite.Application.Features.Bookings.Services;
using SalsCaterSuite.Application.Services;
using SalsCaterSuite.Domain.Entities;
using SalsCaterSuite.Domain.Enums;
using SalsCaterSuite.UnitTests.Common.Fakes;
using Xunit;

/// <summary>
/// <c>PUT /api/bookings/{id}/menus</c> — the full-replace dish selection (contract §15.2,
/// DEC-134, BR-15, BR-35, BR-36, BR-37).
/// <para>
/// The write validates against the tier's <b>live</b> ruleset, refuses after the menu-change
/// window, resurrects a re-chosen dish, soft-deletes an omitted one, and <b>never</b> moves the
/// money.
/// </para>
/// </summary>
public class ReplaceBookingMenuSelectionCommandTests
{
    private static readonly DateOnly Today = new(2026, 10, 22);

    private const int AppetizerCourseId = 1;
    private const int MainCourseCourseId = 2;
    private const int DessertCourseId = 3;

    private readonly FakeSalsCaterSuiteDataStore _store = new();
    private readonly FakeBookingRepository _bookings;
    private readonly FakeBookingPolicyProvider _policy = new();
    private readonly ReplaceBookingMenuSelectionCommand _command;

    public ReplaceBookingMenuSelectionCommandTests()
    {
        _bookings = new FakeBookingRepository(_store);

        var rules = new FakePackageCourseRuleRepository(_store);
        var menus = new FakeMenuRepository(_store);

        var courseSets = new BookingMenuCourseSetBuilder(rules, menus);

        _command = new ReplaceBookingMenuSelectionCommand(
            _bookings,
            new BookingLineItemResolver(courseSets, new FakeAddonRepository(_store)),
            new BookingMenuSelectionPresenter(
                courseSets,
                new BookingTierEstimateService(new FakePackageRepository(_store), rules, menus),
                new BookingAmountDueCalculator(),
                _policy,
                new FakeDateTime(Today.ToDateTime(TimeOnly.MinValue))),
            _policy,
            new FakeDateTime(Today.ToDateTime(TimeOnly.MinValue)));

        SeedCatalogue();
    }

    // ─────────────────────────── the fixtures ───────────────────────────

    private void SeedCatalogue()
    {
        _store.CourseCategories.Add(new CourseCategory { CourseId = AppetizerCourseId, CourseName = "Appetizer" });
        _store.CourseCategories.Add(new CourseCategory { CourseId = MainCourseCourseId, CourseName = "Main Course" });
        _store.CourseCategories.Add(new CourseCategory { CourseId = DessertCourseId, CourseName = "Dessert" });

        _store.Packages.Add(new Package
        {
            PackageId = 10,
            PackageType = "Regular",
            Description = "2026 Premier Package B",
            AmountPax = 500m,
            IsActive = true
        });

        // The tier's ruleset: exactly one appetizer and exactly two main courses.
        _store.PackageCourseRules.Add(new PackageCourseRule
        {
            PackageCourseRuleId = 1,
            PackageId = 10,
            CourseId = AppetizerCourseId,
            RequiredQuota = 1,
            PortionGrams = 150,
            RuleDescription = "One appetizer per event"
        });

        _store.PackageCourseRules.Add(new PackageCourseRule
        {
            PackageCourseRuleId = 2,
            PackageId = 10,
            CourseId = MainCourseCourseId,
            RequiredQuota = 2,
            PortionGrams = 250,
            RuleDescription = "Two mains per event"
        });

        AddDish("APP-001", AppetizerCourseId, "Lumpia", foodCost: 60m, note: "Fried to order");
        AddDish("APP-002", AppetizerCourseId, "Calamares", foodCost: 80m);
        AddDish("MAIN-001", MainCourseCourseId, "Roast Beef", foodCost: 125m, note: "Medium rare");
        AddDish("MAIN-002", MainCourseCourseId, "Beef Steak", foodCost: 90m);
        AddDish("MAIN-003", MainCourseCourseId, "Chicken Cordon Bleu", foodCost: 100m);
        AddDish("DES-001", DessertCourseId, "Leche Flan", foodCost: 40m);
    }

    private void AddDish(string menuId, int courseId, string name, decimal foodCost, string? note = null)
    {
        _store.Menus.Add(new Menu
        {
            MenuId = menuId,
            CourseId = courseId,
            MenuName = name,
            Price = 300m,
            FoodCost = foodCost,
            Note = note
        });
    }

    private Booking SeedBooking(
        int? packageId = 10,
        int eventDateOffsetDays = 60,
        BookingStatus status = BookingStatus.Confirmed)
    {
        var eventDate = Today.AddDays(eventDateOffsetDays);
        var start = eventDate.ToDateTime(new TimeOnly(17, 0));

        var booking = Booking.Create(
            customerId: 1,
            noOfPerson: 100,
            bookType: BookingType.Inside,
            typeOfService: ServiceType.Catering,
            eventDate: eventDate,
            startDate: start,
            endDate: start.AddHours(5),
            occasion: "Integration Event",
            venue: "Grand Ballroom",
            packageId: packageId,
            packageName: packageId.HasValue ? "2026 Premier Package B" : null,
            pricePerHead: 500m);

        booking.RecalculateFinancials(0m, 0m, 2_000m, 0m);

        if (status != BookingStatus.Pending)
        {
            booking.Confirm();
        }

        _store.Bookings.Add(booking);
        _store.AssignTransactionId(booking);
        booking.AssignBookingNumber();

        return booking;
    }

    private static BookingMenuSelectionEntryDto Entry(
        string menuId,
        int courseId,
        string? prepInstruction = null) =>
        new() { MenuId = menuId, CourseId = courseId, PrepInstruction = prepInstruction };

    private static BookingMenuSelectionRequest Request(params BookingMenuSelectionEntryDto[] entries) =>
        new() { Selections = entries.ToList() };

    private static BookingMenuSelectionRequest CompliantSelection(params BookingMenuSelectionEntryDto[] extra) =>
        Request(new[]
        {
            Entry("APP-001", AppetizerCourseId),
            Entry("MAIN-001", MainCourseCourseId),
            Entry("MAIN-002", MainCourseCourseId)
        }.Concat(extra).ToArray());

    /// <summary>
    /// The compliant selection again, with a prep instruction hung on the appetizer.
    /// </summary>
    private static BookingMenuSelectionRequest CompliantSelectionWithAppetizerPrep(string? prepInstruction) =>
        Request(
            Entry("APP-001", AppetizerCourseId, prepInstruction),
            Entry("MAIN-001", MainCourseCourseId),
            Entry("MAIN-002", MainCourseCourseId));

    // ─────────────────────────── the happy path ───────────────────────────

    [Fact]
    public async Task A_compliant_selection_should_be_stored_and_described()
    {
        var booking = SeedBooking();

        var result = await _command.ExecuteAsync(booking.TransactionId, CompliantSelection());

        result.Succeeded.Should().BeTrue();

        var dto = result.Data!;
        dto.TransactionId.Should().Be(booking.TransactionId);
        dto.Selections.Should().HaveCount(3);
        dto.IsCompliant.Should().BeTrue();
        dto.ComplianceMessage.Should().BeNull();
        dto.SelectionState.Should().Be(BookingSelectionState.Selected);
        dto.Courses.Should().HaveCount(2);
        dto.Courses.Should().OnlyContain(course =>
            course.ChosenMenuIds.Count == course.RequiredQuota);

        booking.Menus.Should().HaveCount(3);
        booking.Menus.Should().OnlyContain(row => !row.IsDeleted);
        booking.Menus.Should().OnlyContain(row => row.TransactionId == booking.TransactionId);
    }

    /// <summary>
    /// BR-37: the selection carries no money and never moves the booking's totals.
    /// </summary>
    [Fact]
    public async Task The_write_should_never_move_the_money()
    {
        var booking = SeedBooking();
        var amountDue = booking.AmountDue;

        await _command.ExecuteAsync(booking.TransactionId, CompliantSelection());

        booking.AmountDue.Should().Be(amountDue);
        booking.DiscountAmount.Should().Be(2_000m);
        booking.AddonsTotal.Should().Be(0m);
    }

    [Fact]
    public async Task The_prep_instruction_should_be_trimmed()
    {
        var booking = SeedBooking();

        var result = await _command.ExecuteAsync(
            booking.TransactionId,
            CompliantSelectionWithAppetizerPrep("  no garlic  "));

        result.Succeeded.Should().BeTrue();
        booking.Menus.Single(row => row.MenuId == "APP-001").PrepInstruction.Should().Be("no garlic");
    }

    [Fact]
    public async Task A_blank_prep_instruction_should_be_stored_as_not_stated()
    {
        var booking = SeedBooking();

        await _command.ExecuteAsync(
            booking.TransactionId,
            CompliantSelectionWithAppetizerPrep("   "));

        booking.Menus.Single(row => row.MenuId == "APP-001").PrepInstruction.Should().BeNull();
    }

    [Fact]
    public async Task An_omitted_prep_instruction_should_clear_the_stored_one()
    {
        var booking = SeedBooking();
        await _command.ExecuteAsync(
            booking.TransactionId,
            CompliantSelectionWithAppetizerPrep("no garlic"));

        await _command.ExecuteAsync(booking.TransactionId, CompliantSelection());

        booking.Menus.Single(row => row.MenuId == "APP-001").PrepInstruction
            .Should().BeNull("a full replace re-states the whole selection (DEC-155)");
    }

    // ─────────────────────────── the replace semantics ───────────────────────────

    [Fact]
    public async Task Omitting_a_dish_should_soft_delete_it_and_keep_the_row()
    {
        var booking = SeedBooking();
        await _command.ExecuteAsync(booking.TransactionId, CompliantSelection());

        var result = await _command.ExecuteAsync(
            booking.TransactionId,
            Request(
                Entry("APP-002", AppetizerCourseId),
                Entry("MAIN-001", MainCourseCourseId),
                Entry("MAIN-002", MainCourseCourseId)));

        result.Succeeded.Should().BeTrue();
        result.Data!.Selections.Should().ContainSingle(item => item.MenuId == "APP-002");

        booking.Menus.Should().HaveCount(4, "the replaced row is kept, soft-deleted (DEC-085)");
        booking.Menus.Single(row => row.MenuId == "APP-001").IsDeleted.Should().BeTrue();
        booking.Menus.Single(row => row.MenuId == "APP-002").IsDeleted.Should().BeFalse();
    }

    [Fact]
    public async Task Re_choosing_a_dish_should_resurrect_its_row()
    {
        var booking = SeedBooking();
        await _command.ExecuteAsync(booking.TransactionId, CompliantSelection());
        await _command.ExecuteAsync(booking.TransactionId, Request(Entry("MAIN-001", MainCourseCourseId)));

        await _command.ExecuteAsync(booking.TransactionId, CompliantSelection());

        booking.Menus.Should().HaveCount(3, "a re-chosen dish resurrects rather than duplicating (DEC-134)");
        booking.Menus.Should().OnlyContain(row => !row.IsDeleted);
    }

    [Fact]
    public async Task An_empty_array_should_clear_the_selection()
    {
        var booking = SeedBooking();
        await _command.ExecuteAsync(booking.TransactionId, CompliantSelection());

        var result = await _command.ExecuteAsync(booking.TransactionId, Request());

        result.Succeeded.Should().BeTrue();
        result.Data!.Selections.Should().BeEmpty();
        result.Data.SelectionState.Should().Be(BookingSelectionState.None, "no live row remains (BR-28, M2)");
        result.Data.IsCompliant.Should().BeTrue("an empty selection is 'no selection' (DEC-131)");
        result.Data.ComplianceMessage.Should().BeNull();
        booking.Menus.Should().OnlyContain(row => row.IsDeleted);
    }

    /// <summary>
    /// DEC-085: an absent member is "nothing was submitted", never "clear everything".
    /// </summary>
    [Fact]
    public async Task An_absent_member_should_be_rejected()
    {
        var booking = SeedBooking();

        var result = await _command.ExecuteAsync(booking.TransactionId, new BookingMenuSelectionRequest());

        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain(
            "The 'selections' member is required. Send an empty array to clear the selection.");
        _bookings.SaveCount.Should().Be(0);
    }

    // ─────────────────────────── the BR-15 validation ───────────────────────────

    [Fact]
    public async Task An_under_quota_course_should_be_refused()
    {
        var booking = SeedBooking();

        var result = await _command.ExecuteAsync(
            booking.TransactionId,
            Request(Entry("APP-001", AppetizerCourseId), Entry("MAIN-001", MainCourseCourseId)));

        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain(
            "The 'Main Course' course requires exactly 2 dish(es); 1 selected.");
        booking.Menus.Should().BeEmpty("nothing is written");
    }

    [Fact]
    public async Task An_over_quota_course_should_be_refused()
    {
        var booking = SeedBooking();

        var result = await _command.ExecuteAsync(
            booking.TransactionId,
            CompliantSelection(Entry("MAIN-003", MainCourseCourseId)));

        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain(
            "The 'Main Course' course requires exactly 2 dish(es); 3 selected.");
    }

    [Fact]
    public async Task A_dish_outside_its_course_pool_should_be_refused()
    {
        var booking = SeedBooking();

        // Leche Flan is a dessert, so it is not a live member of the appetizer pool.
        var result = await _command.ExecuteAsync(
            booking.TransactionId,
            Request(Entry("DES-001", AppetizerCourseId), Entry("MAIN-001", MainCourseCourseId), Entry("MAIN-002", MainCourseCourseId)));

        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain(
            "The dish 'DES-001' is not a live member of the 'Appetizer' course's qualified pool.");
    }

    [Fact]
    public async Task An_unruled_course_should_be_refused()
    {
        var booking = SeedBooking();

        var result = await _command.ExecuteAsync(
            booking.TransactionId,
            CompliantSelection(Entry("DES-001", DessertCourseId)));

        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain(
            "The selected dishes name the 'Dessert' course, which the tier holds no course rule " +
            "for. Remove them or add the course to the tier.");
    }

    [Fact]
    public async Task An_unknown_dish_should_be_refused()
    {
        var booking = SeedBooking();

        var result = await _command.ExecuteAsync(
            booking.TransactionId,
            CompliantSelection(Entry("NOPE-001", AppetizerCourseId)));

        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain("The dish 'NOPE-001' was not found in the menu catalogue.");
    }

    [Fact]
    public async Task A_dish_chosen_twice_should_be_refused()
    {
        var booking = SeedBooking();

        var result = await _command.ExecuteAsync(
            booking.TransactionId,
            CompliantSelection(Entry("MAIN-001", MainCourseCourseId)));

        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain("The dish 'MAIN-001' is selected more than once.");
    }

    [Fact]
    public async Task A_blank_dish_code_should_be_refused()
    {
        var booking = SeedBooking();

        var result = await _command.ExecuteAsync(
            booking.TransactionId,
            CompliantSelection(Entry("   ", AppetizerCourseId)));

        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain("Every selected dish must name a menu.");
    }

    [Fact]
    public async Task A_prep_instruction_over_five_hundred_characters_should_be_refused()
    {
        var booking = SeedBooking();

        var result = await _command.ExecuteAsync(
            booking.TransactionId,
            CompliantSelectionWithAppetizerPrep(new string('x', 501)));

        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain(
            "The prep instruction for dish 'APP-001' must not exceed 500 characters.");
    }

    /// <summary>
    /// A bespoke booking carries no tier, therefore no ruleset — so there is nothing to validate
    /// a selection against (contract §15.2).
    /// </summary>
    [Fact]
    public async Task A_selection_on_a_bespoke_booking_should_be_refused()
    {
        var booking = SeedBooking(packageId: null);

        var result = await _command.ExecuteAsync(booking.TransactionId, CompliantSelection());

        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain("A dish selection requires a catering package.");
    }

    // ─────────────────────────── the window and the lifecycle ───────────────────────────

    [Fact]
    public async Task A_locked_window_should_refuse_the_write_and_name_the_lock_date()
    {
        var booking = SeedBooking(eventDateOffsetDays: 3);
        _policy.MenuSelectionLockDaysBeforeEvent = 7;

        var result = await _command.ExecuteAsync(booking.TransactionId, CompliantSelection());

        result.Succeeded.Should().BeFalse();
        result.Errors.Single().Should().StartWith(
            "The dish selection cannot be changed: the menu-change window closed on ");
        result.Errors.Single().Should().Contain(booking.EventDate.AddDays(-7).ToString("yyyy-MM-dd"));
        _bookings.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task An_open_window_should_accept_the_write()
    {
        var booking = SeedBooking(eventDateOffsetDays: 3);
        _policy.MenuSelectionLockDaysBeforeEvent = 2;

        var result = await _command.ExecuteAsync(booking.TransactionId, CompliantSelection());

        result.Succeeded.Should().BeTrue();
        result.Data!.IsLocked.Should().BeFalse();
        result.Data.MenuSelectionLockDate.Should().Be(booking.EventDate.AddDays(-2));
    }

    [Fact]
    public async Task A_cancelled_booking_should_refuse_the_write()
    {
        var booking = SeedBooking();
        booking.Cancel();

        var result = await _command.ExecuteAsync(booking.TransactionId, CompliantSelection());

        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain("A cancelled booking's dish selection cannot be changed.");
    }

    [Fact]
    public async Task An_unknown_booking_should_be_reported_as_not_found()
    {
        var result = await _command.ExecuteAsync(9_999, CompliantSelection());

        result.Should().BeOfType<BookingNotFoundResult<BookingMenuSelectionDto>>();
        result.Errors.Should().Contain("Booking not found.");
    }

    // ─────────────────────────── the derived read fields ───────────────────────────

    [Fact]
    public async Task The_selected_food_cost_should_be_the_sum_of_the_chosen_dishes()
    {
        var booking = SeedBooking();

        var result = await _command.ExecuteAsync(booking.TransactionId, CompliantSelection());

        // Lumpia 60 + Roast Beef 125 + Beef Steak 90.
        result.Data!.SelectedFoodCostPerPax.Should().Be(275m);
    }

    [Fact]
    public async Task An_uncosted_chosen_dish_should_make_the_selected_food_cost_unknown()
    {
        var booking = SeedBooking();
        _store.Menus.Single(menu => menu.MenuId == "MAIN-002").FoodCost = null;

        var result = await _command.ExecuteAsync(booking.TransactionId, CompliantSelection());

        result.Data!.SelectedFoodCostPerPax.Should().BeNull("an unknown cost is never zero (DEC-087)");
        result.Data.SelectedGrossProfitPercentage.Should().BeNull();
    }

    [Fact]
    public async Task The_batch_mass_should_be_the_portion_times_the_headcount()
    {
        var booking = SeedBooking();

        var result = await _command.ExecuteAsync(booking.TransactionId, CompliantSelection());

        var mainCourse = result.Data!.Courses.Single(course => course.CourseName == "Main Course");
        mainCourse.BatchMassKg.Should().Be(25m, "250g x 100 heads / 1000");

        var appetizer = result.Data.Courses.Single(course => course.CourseName == "Appetizer");
        appetizer.BatchMassKg.Should().Be(15m, "150g x 100 heads / 1000");
    }

    [Fact]
    public async Task The_standard_run_estimate_should_be_published_beside_the_selection()
    {
        var booking = SeedBooking();

        var result = await _command.ExecuteAsync(booking.TransactionId, CompliantSelection());

        // Appetizer pool mean (60 + 80) / 2 = 70; main pool mean (125 + 90 + 100) / 3 = 105.
        result.Data!.StandardRunFoodCostPerPax.Should().Be(175m);
    }
}
