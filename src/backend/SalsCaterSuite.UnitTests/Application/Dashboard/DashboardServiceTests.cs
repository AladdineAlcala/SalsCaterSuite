namespace SalsCaterSuite.UnitTests.Application.Dashboard;

using FluentAssertions;
using SalsCaterSuite.Application.Features.Dashboard.DTOs.Response;
using SalsCaterSuite.Application.Features.Dashboard.Models;
using SalsCaterSuite.Domain.Enums;
using Xunit;

/// <summary>
/// The dashboard KPI formulas (spec §6, §12.1). Every expected value is calculated by hand
/// from the fixture — the tests never call production arithmetic to derive an expectation.
/// </summary>
public class DashboardServiceTests
{
    private static readonly DateTime October1 = new(2026, 10, 1, 9, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime September1 = new(2026, 9, 1, 9, 0, 0, DateTimeKind.Utc);

    // ─────────────────────────── KPI-07 / KPI-01 / KPI-04 ───────────────────────────

    [Fact]
    public async Task The_bookings_kpi_should_split_the_period_population_by_commitment()
    {
        var harness = new DashboardHarness();
        SeedCommittedTentativeAndOtherBookings(harness);

        var dashboard = await GetDashboardAsync(harness);

        dashboard.Bookings.TotalCount.Should().Be(6);
        dashboard.Bookings.CommittedCount.Should().Be(3, "only lifecycle Confirmed is committed (DEC-177)");
        dashboard.Bookings.TentativeCount.Should().Be(1, "Pending is tentative");
        dashboard.Bookings.OtherCount.Should().Be(2, "Completed and NoShow are 'other'");
        dashboard.Bookings.TotalPax.Should().Be(430, "100 + 100 + 50 + 80 + 60 + 40 heads");
    }

    [Fact]
    public async Task The_contracted_value_should_be_the_sum_of_the_net_amount_due_of_the_period_population()
    {
        var harness = new DashboardHarness();
        SeedCommittedTentativeAndOtherBookings(harness);

        var dashboard = await GetDashboardAsync(harness);

        // 50,000 + 30,000 + 20,000 + 20,000 + 36,000 + 4,000
        dashboard.Bookings.ContractedValue.Should().Be(160_000m);
        dashboard.Revenue.NetContractedRevenue.Should().Be(160_000m, "revenue and contracted value share one population");
        dashboard.Revenue.BookingCount.Should().Be(6);
    }

    [Fact]
    public async Task The_average_booking_value_should_be_the_revenue_divided_by_the_booking_count()
    {
        var harness = new DashboardHarness();
        SeedCommittedTentativeAndOtherBookings(harness);

        var dashboard = await GetDashboardAsync(harness);

        // 160,000 ÷ 6 = 26,666.666… → 26,666.67
        dashboard.Revenue.AverageBookingValue.Should().Be(26_666.67m);
    }

    [Fact]
    public async Task The_average_booking_value_should_be_null_when_the_period_has_no_bookings()
    {
        var harness = new DashboardHarness();

        var dashboard = await GetDashboardAsync(harness);

        dashboard.Bookings.TotalCount.Should().Be(0);
        dashboard.Revenue.BookingCount.Should().Be(0);
        dashboard.Revenue.NetContractedRevenue.Should().Be(0m);
        dashboard.Revenue.AverageBookingValue.Should().BeNull("an undefined ratio is null, never zero");
    }

    [Fact]
    public async Task Cancelled_bookings_should_be_excluded_from_the_counts_and_the_revenue()
    {
        var harness = new DashboardHarness();
        SeedCommittedTentativeAndOtherBookings(harness);
        harness.AddBooking(harness.PeriodStart, 200, 500m, BookingStatus.Cancelled);
        harness.AddPayment(7, 50_000m, October1);

        var dashboard = await GetDashboardAsync(harness);

        dashboard.Bookings.TotalCount.Should().Be(6, "a cancelled booking is not in the period population");
        dashboard.Bookings.ContractedValue.Should().Be(160_000m);
        dashboard.Revenue.NetContractedRevenue.Should().Be(160_000m);
        dashboard.Receivables.OutstandingAmount.Should().Be(130_000m, "a cancelled booking leaves AR (BR-06)");
    }

    [Fact]
    public async Task Expired_bookings_should_be_excluded_from_the_period_population()
    {
        var harness = new DashboardHarness();
        SeedCommittedTentativeAndOtherBookings(harness);
        harness.AddBooking(harness.PeriodStart, 100, 500m, BookingStatus.Expired);

        var dashboard = await GetDashboardAsync(harness);

        dashboard.Bookings.TotalCount.Should().Be(6, "an expired booking is not in the period population");
        dashboard.Bookings.ContractedValue.Should().Be(160_000m);
    }

    [Fact]
    public async Task Soft_deleted_bookings_and_payments_should_be_excluded_from_every_figure()
    {
        var harness = new DashboardHarness();
        SeedCommittedTentativeAndOtherBookings(harness);

        // A soft-deleted booking: excluded from the population, the revenue and AR.
        harness.AddBooking(harness.PeriodStart, 100, 500m, BookingStatus.Confirmed, isDeleted: true);

        // A soft-deleted payment on a live booking: excluded from the collections and from
        // the authoritative amount paid of that booking.
        harness.AddPayment(2, 5_000m, October1, isDeleted: true);

        var dashboard = await GetDashboardAsync(harness);

        dashboard.Bookings.TotalCount.Should().Be(6, "the soft-deleted booking is not in the population");
        dashboard.Bookings.ContractedValue.Should().Be(160_000m);
        dashboard.Revenue.NetContractedRevenue.Should().Be(160_000m);
        dashboard.Receivables.OutstandingAmount.Should().Be(130_000m, "booking 2 still carries a 20,000 balance");
        dashboard.Collections.Amount.Should().Be(30_000m, "the soft-deleted payment is excluded from the collections");
    }

    [Fact]
    public async Task The_other_bucket_should_also_cover_in_progress_and_rescheduled_bookings()
    {
        var harness = new DashboardHarness();
        harness.AddBooking(harness.PeriodStart, 10, 100m, BookingStatus.InProgress);
        harness.AddBooking(harness.PeriodStart, 10, 100m, BookingStatus.Rescheduled);

        var dashboard = await GetDashboardAsync(harness);

        dashboard.Bookings.TotalCount.Should().Be(2);
        dashboard.Bookings.OtherCount.Should().Be(2);
        dashboard.Bookings.CommittedCount.Should().Be(0);
        dashboard.Bookings.TentativeCount.Should().Be(0);
    }

    // ─────────────────────────── KPI-02 / KPI-11 ───────────────────────────

    [Fact]
    public async Task The_collections_should_be_the_sum_of_the_payments_made_in_the_period()
    {
        var harness = new DashboardHarness();
        SeedCommittedTentativeAndOtherBookings(harness);
        harness.AddPayment(1, 10_000m, new DateTime(2026, 10, 5, 9, 0, 0, DateTimeKind.Utc));
        harness.AddPayment(1, 25_000m, new DateTime(2026, 10, 12, 9, 0, 0, DateTimeKind.Utc));
        harness.AddPayment(1, 5_000m, new DateTime(2026, 10, 20, 9, 0, 0, DateTimeKind.Utc));
        // The previous period.
        harness.AddPayment(1, 20_000m, new DateTime(2026, 9, 10, 9, 0, 0, DateTimeKind.Utc));

        var dashboard = await GetDashboardAsync(harness);

        // 10,000 + 20,000 (the fixture's own payments) + 10,000 + 25,000 + 5,000
        dashboard.Collections.Amount.Should().Be(70_000m);
        dashboard.Collections.ReceiptCount.Should().Be(5);
        dashboard.Collections.PreviousPeriodAmount.Should().Be(20_000m, "the previous calendar month");
        dashboard.Collections.ChangePercentage.Should().Be(250.00m, "(70,000 − 20,000) ÷ 20,000 × 100");
        dashboard.Collections.CollectionRatioPercentage.Should().Be(43.75m, "70,000 ÷ 160,000 × 100");
    }

    [Fact]
    public async Task The_change_percentage_should_be_null_when_the_previous_period_collected_nothing()
    {
        var harness = new DashboardHarness();
        SeedCommittedTentativeAndOtherBookings(harness);

        var dashboard = await GetDashboardAsync(harness);

        dashboard.Collections.Amount.Should().Be(30_000m);
        dashboard.Collections.PreviousPeriodAmount.Should().Be(0m);
        dashboard.Collections.ChangePercentage.Should().BeNull("an undefined ratio is null, never zero");
    }

    [Fact]
    public async Task The_collection_ratio_should_be_null_when_the_period_booked_no_revenue()
    {
        var harness = new DashboardHarness();
        harness.AddBooking(harness.PeriodStart, 10, 0m, BookingStatus.Confirmed);
        harness.AddPayment(1, 5_000m, new DateTime(2026, 10, 5, 9, 0, 0, DateTimeKind.Utc));

        var dashboard = await GetDashboardAsync(harness);

        dashboard.Revenue.NetContractedRevenue.Should().Be(0m);
        dashboard.Collections.Amount.Should().Be(5_000m);
        dashboard.Collections.CollectionRatioPercentage.Should().BeNull("the denominator is zero");
    }

    // ─────────────────────────── KPI-12 ───────────────────────────

    [Fact]
    public async Task The_receivables_should_be_the_sum_of_the_balance_of_every_non_cancelled_booking()
    {
        var harness = new DashboardHarness();
        SeedCommittedTentativeAndOtherBookings(harness);
        harness.AddBooking(harness.PeriodStart, 200, 500m, BookingStatus.Cancelled);
        harness.AddPayment(7, 50_000m, October1);

        var dashboard = await GetDashboardAsync(harness);

        // 50,000 (1) + 20,000 (2) + 0 (3, fully paid) + 20,000 (4) + 36,000 (5) + 4,000 (6)
        dashboard.Receivables.OutstandingAmount.Should().Be(130_000m);
        dashboard.Receivables.BookingCount.Should().Be(5, "a fully paid booking contributes no balance");
    }

    [Fact]
    public async Task A_completed_booking_with_an_outstanding_balance_should_remain_in_the_receivables()
    {
        var harness = new DashboardHarness();
        harness.AddBooking(harness.Today, 60, 600m, BookingStatus.Completed);

        var dashboard = await GetDashboardAsync(harness);

        dashboard.Receivables.OutstandingAmount.Should().Be(36_000m, "a served event is still collectible (DEC-025)");
        dashboard.Receivables.BookingCount.Should().Be(1);
    }

    // ─────────────────────────── KPI-13 (aging) ───────────────────────────

    [Fact]
    public async Task The_aging_buckets_should_follow_the_event_date_boundaries()
    {
        var harness = new DashboardHarness();

        // today = 2026-10-19. Each booking is one day-boundary case with a distinct amount.
        harness.AddBooking(new DateOnly(2026, 10, 19), 1, 100m, BookingStatus.Confirmed); // 0 days  → not yet due
        harness.AddBooking(new DateOnly(2026, 10, 18), 1, 200m, BookingStatus.Confirmed); // 1 day
        harness.AddBooking(new DateOnly(2026, 9, 19), 1, 300m, BookingStatus.Confirmed);  // 30 days
        harness.AddBooking(new DateOnly(2026, 9, 18), 1, 400m, BookingStatus.Confirmed);  // 31 days
        harness.AddBooking(new DateOnly(2026, 8, 20), 1, 500m, BookingStatus.Confirmed);  // 60 days
        harness.AddBooking(new DateOnly(2026, 8, 19), 1, 600m, BookingStatus.Confirmed);  // 61 days
        harness.AddBooking(new DateOnly(2026, 7, 21), 1, 700m, BookingStatus.Confirmed);  // 90 days
        harness.AddBooking(new DateOnly(2026, 7, 20), 1, 800m, BookingStatus.Confirmed);  // 91 days

        var dashboard = await GetDashboardAsync(harness);

        var buckets = dashboard.Receivables.AgingBuckets;
        buckets.Should().HaveCount(5);

        buckets[0].Key.Should().Be("NotYetDue");
        buckets[0].BookingCount.Should().Be(1);
        buckets[0].Amount.Should().Be(100m);

        buckets[1].Key.Should().Be("Days1To30");
        buckets[1].BookingCount.Should().Be(2, "1 day and 30 days");
        buckets[1].Amount.Should().Be(500m, "200 + 300");

        buckets[2].Key.Should().Be("Days31To60");
        buckets[2].BookingCount.Should().Be(2, "31 days and 60 days");
        buckets[2].Amount.Should().Be(900m, "400 + 500");

        buckets[3].Key.Should().Be("Days61To90");
        buckets[3].BookingCount.Should().Be(2, "61 days and 90 days");
        buckets[3].Amount.Should().Be(1_300m, "600 + 700");

        buckets[4].Key.Should().Be("Over90Days");
        buckets[4].BookingCount.Should().Be(1, "91 days");
        buckets[4].Amount.Should().Be(800m);
    }

    [Fact]
    public async Task The_aging_bucket_amounts_should_sum_exactly_to_the_outstanding_receivables()
    {
        var harness = new DashboardHarness();
        harness.AddBooking(new DateOnly(2026, 10, 19), 1, 100m, BookingStatus.Confirmed);
        harness.AddBooking(new DateOnly(2026, 10, 18), 1, 200m, BookingStatus.Confirmed);
        harness.AddBooking(new DateOnly(2026, 9, 19), 1, 300m, BookingStatus.Confirmed);
        harness.AddBooking(new DateOnly(2026, 9, 18), 1, 400m, BookingStatus.Confirmed);
        harness.AddBooking(new DateOnly(2026, 8, 20), 1, 500m, BookingStatus.Confirmed);
        harness.AddBooking(new DateOnly(2026, 8, 19), 1, 600m, BookingStatus.Confirmed);
        harness.AddBooking(new DateOnly(2026, 7, 21), 1, 700m, BookingStatus.Confirmed);
        harness.AddBooking(new DateOnly(2026, 7, 20), 1, 800m, BookingStatus.Confirmed);
        // A settled booking must not land in any bucket.
        harness.AddBooking(new DateOnly(2026, 7, 20), 1, 900m, BookingStatus.Confirmed);
        harness.AddPayment(9, 900m, October1);

        var dashboard = await GetDashboardAsync(harness);

        dashboard.Receivables.AgingBuckets.Sum(b => b.Amount)
            .Should().Be(dashboard.Receivables.OutstandingAmount, "the buckets must reconcile to the cent");
        dashboard.Receivables.AgingBuckets.Sum(b => b.BookingCount)
            .Should().Be(dashboard.Receivables.BookingCount, "every booking lands in exactly one bucket");
        dashboard.Receivables.OutstandingAmount.Should().Be(3_600m);
    }

    [Fact]
    public async Task The_overdue_figures_should_cover_everything_more_than_thirty_days_past_the_event_date()
    {
        var harness = new DashboardHarness();
        harness.AddBooking(new DateOnly(2026, 10, 19), 1, 100m, BookingStatus.Confirmed);
        harness.AddBooking(new DateOnly(2026, 10, 18), 1, 200m, BookingStatus.Confirmed);
        harness.AddBooking(new DateOnly(2026, 9, 19), 1, 300m, BookingStatus.Confirmed);
        harness.AddBooking(new DateOnly(2026, 9, 18), 1, 400m, BookingStatus.Confirmed);
        harness.AddBooking(new DateOnly(2026, 8, 20), 1, 500m, BookingStatus.Confirmed);
        harness.AddBooking(new DateOnly(2026, 8, 19), 1, 600m, BookingStatus.Confirmed);
        harness.AddBooking(new DateOnly(2026, 7, 21), 1, 700m, BookingStatus.Confirmed);
        harness.AddBooking(new DateOnly(2026, 7, 20), 1, 800m, BookingStatus.Confirmed);

        var dashboard = await GetDashboardAsync(harness);

        dashboard.Receivables.OverdueCount.Should().Be(5, "31–60, 61–90 and 90+ days");
        dashboard.Receivables.OverdueAmount.Should().Be(3_000m, "900 + 1,300 + 800");
    }

    // ─────────────────────────── KPI-14 / KPI-15 ───────────────────────────

    [Fact]
    public async Task The_dish_costing_coverage_should_measure_the_costed_share_of_the_catalogue()
    {
        var harness = new DashboardHarness();
        harness.AddCourse(1, "Beef Entree");
        harness.AddCourse(2, "Chicken Entree");
        harness.AddMenu(1, 350m, 125m);   // costed
        harness.AddMenu(1, 300m, 90m);    // costed
        harness.AddMenu(2, 200m, null);   // uncosted
        harness.AddMenu(2, 150m, 0m);     // uncosted
        harness.AddMenu(null, 100m, -5m); // uncosted
        harness.AddMenu(1, 500m, 100m, isDeleted: true); // soft-deleted

        var dashboard = await GetDashboardAsync(harness);

        dashboard.DishCosting.TotalMenuCount.Should().Be(5, "the soft-deleted menu is excluded");
        dashboard.DishCosting.CostedMenuCount.Should().Be(2);
        dashboard.DishCosting.UncostedMenuCount.Should().Be(3);
        dashboard.DishCosting.CoveragePercentage.Should().Be(40.00m, "2 ÷ 5 × 100");
    }

    [Fact]
    public async Task The_catalogue_margin_should_be_measured_over_the_costed_and_priced_menus()
    {
        var harness = new DashboardHarness();
        harness.AddCourse(1, "Beef Entree");
        harness.AddMenu(1, 350m, 125m);
        harness.AddMenu(1, 300m, 90m);

        var dashboard = await GetDashboardAsync(harness);

        dashboard.Margin.CatalogueRevenueAmount.Should().Be(650m, "350 + 300");
        dashboard.Margin.CatalogueFoodCostAmount.Should().Be(215m, "125 + 90");
        dashboard.Margin.CatalogueMarginAmount.Should().Be(435m);
        dashboard.Margin.CatalogueMarginPercentage.Should().Be(66.92m, "435 ÷ 650 × 100");
        dashboard.Margin.VarianceToBenchmarkPercentage.Should().Be(1.92m, "66.92 − 65.00");
    }

    [Fact]
    public async Task The_catalogue_margin_should_be_null_when_the_catalogue_has_no_revenue()
    {
        var harness = new DashboardHarness();
        harness.AddMenu(null, null, 50m);

        var dashboard = await GetDashboardAsync(harness);

        dashboard.Margin.CatalogueRevenueAmount.Should().Be(0m);
        dashboard.Margin.CatalogueMarginPercentage.Should().BeNull();
        dashboard.Margin.VarianceToBenchmarkPercentage.Should().BeNull("there is no margin to compare");
    }

    // ─────────────────────────── KPI-16 ───────────────────────────

    [Fact]
    public async Task The_food_cost_by_category_should_report_the_share_of_each_course()
    {
        var harness = new DashboardHarness();
        harness.AddCourse(1, "Beef Entree");
        harness.AddCourse(2, "Chicken Entree");
        harness.AddMenu(1, 350m, 100m);
        harness.AddMenu(1, 350m, 200m);
        harness.AddMenu(1, 350m, 300m);
        harness.AddMenu(2, 200m, 100m);
        harness.AddMenu(2, 200m, 100m);
        harness.AddMenu(null, 100m, 50m);

        var dashboard = await GetDashboardAsync(harness);

        dashboard.FoodCostByCategory.Should().HaveCount(3);

        dashboard.FoodCostByCategory[0].CourseId.Should().Be(1);
        dashboard.FoodCostByCategory[0].CourseName.Should().Be("Beef Entree");
        dashboard.FoodCostByCategory[0].FoodCostAmount.Should().Be(600m);
        dashboard.FoodCostByCategory[0].SharePercentage.Should().Be(70.59m, "600 ÷ 850 × 100");
        dashboard.FoodCostByCategory[0].MenuCount.Should().Be(3);

        dashboard.FoodCostByCategory[1].CourseId.Should().Be(2);
        dashboard.FoodCostByCategory[1].CourseName.Should().Be("Chicken Entree");
        dashboard.FoodCostByCategory[1].FoodCostAmount.Should().Be(200m);
        dashboard.FoodCostByCategory[1].SharePercentage.Should().Be(23.53m, "200 ÷ 850 × 100");
        dashboard.FoodCostByCategory[1].MenuCount.Should().Be(2);

        dashboard.FoodCostByCategory[2].CourseId.Should().BeNull();
        dashboard.FoodCostByCategory[2].CourseName.Should().Be("Uncategorised");
        dashboard.FoodCostByCategory[2].FoodCostAmount.Should().Be(50m);
        dashboard.FoodCostByCategory[2].SharePercentage.Should().Be(5.88m, "50 ÷ 850 × 100");
        dashboard.FoodCostByCategory[2].MenuCount.Should().Be(1);
    }

    [Fact]
    public async Task An_uncosted_course_should_not_appear_in_the_food_cost_breakdown()
    {
        var harness = new DashboardHarness();
        harness.AddCourse(1, "Beef Entree");
        harness.AddCourse(2, "Chicken Entree");
        harness.AddMenu(1, 350m, 100m);
        harness.AddMenu(2, 200m, null);

        var dashboard = await GetDashboardAsync(harness);

        dashboard.FoodCostByCategory.Should().HaveCount(1);
        dashboard.FoodCostByCategory[0].CourseId.Should().Be(1);
    }

    // ─────────────────────────── KPI-17 / KPI-18 / KPI-19 ───────────────────────────

    [Fact]
    public async Task The_trailing_seven_day_collections_should_cover_the_last_seven_calendar_days()
    {
        var harness = new DashboardHarness();
        harness.AddPayment(1, 1_000m, new DateTime(2026, 10, 13, 8, 0, 0, DateTimeKind.Utc));
        harness.AddPayment(1, 2_000m, new DateTime(2026, 10, 15, 8, 0, 0, DateTimeKind.Utc));
        harness.AddPayment(1, 3_000m, new DateTime(2026, 10, 19, 8, 0, 0, DateTimeKind.Utc));
        harness.AddPayment(1, 5_000m, new DateTime(2026, 10, 12, 23, 59, 0, DateTimeKind.Utc)); // outside
        harness.AddPayment(1, 7_000m, new DateTime(2026, 10, 20, 0, 0, 0, DateTimeKind.Utc));    // outside

        var dashboard = await GetDashboardAsync(harness);

        dashboard.Ledger.CollectedLast7DaysAmount.Should().Be(6_000m, "1,000 + 2,000 + 3,000");
        dashboard.Ledger.CollectedLast7DaysReceiptCount.Should().Be(3);
    }

    [Fact]
    public async Task The_awaiting_deposit_tile_should_count_the_bookings_without_a_payment()
    {
        var harness = new DashboardHarness();
        SeedCommittedTentativeAndOtherBookings(harness);
        harness.AddBooking(harness.PeriodStart, 200, 500m, BookingStatus.Cancelled); // excluded
        harness.AddBooking(harness.PeriodStart, 100, 500m, BookingStatus.Confirmed, isDeleted: true); // excluded

        var dashboard = await GetDashboardAsync(harness);

        // Bookings 1, 4, 5 and 6 have no payment: 50,000 + 20,000 + 36,000 + 4,000
        dashboard.Ledger.AwaitingDepositBookingCount.Should().Be(4);
        dashboard.Ledger.AwaitingDepositAmount.Should().Be(110_000m);
    }

    [Fact]
    public async Task The_receipts_issued_count_should_measure_the_payment_rows_of_the_period()
    {
        var harness = new DashboardHarness();
        harness.AddPayment(1, 10_000m, new DateTime(2026, 10, 5, 9, 0, 0, DateTimeKind.Utc));
        harness.AddPayment(1, 20_000m, September1);

        var dashboard = await GetDashboardAsync(harness);

        dashboard.Ledger.ReceiptsIssuedCount.Should().Be(1);
        dashboard.Collections.ReceiptCount.Should().Be(1);
    }

    // ─────────────────────────── the upcoming schedule ───────────────────────────

    [Fact]
    public async Task The_upcoming_bookings_should_be_ordered_by_event_date_and_capped_at_five()
    {
        var harness = new DashboardHarness();
        harness.AddBooking(new DateOnly(2026, 10, 19), 10, 100m, BookingStatus.Confirmed, transactionId: 10);
        harness.AddBooking(new DateOnly(2026, 10, 19), 10, 100m, BookingStatus.Confirmed, transactionId: 3);
        harness.AddBooking(new DateOnly(2026, 10, 20), 10, 100m, BookingStatus.Confirmed, transactionId: 1);
        harness.AddBooking(new DateOnly(2026, 10, 25), 10, 100m, BookingStatus.Confirmed, transactionId: 2);
        harness.AddBooking(new DateOnly(2026, 10, 26), 10, 100m, BookingStatus.Confirmed, transactionId: 4);
        harness.AddBooking(new DateOnly(2026, 11, 1), 10, 100m, BookingStatus.Confirmed, transactionId: 5);
        harness.AddBooking(new DateOnly(2026, 11, 2), 10, 100m, BookingStatus.Confirmed, transactionId: 6);
        // Excluded from the schedule.
        harness.AddBooking(new DateOnly(2026, 10, 22), 10, 100m, BookingStatus.Cancelled, transactionId: 7);
        harness.AddBooking(new DateOnly(2026, 10, 22), 10, 100m, BookingStatus.Expired, transactionId: 8);
        harness.AddBooking(new DateOnly(2026, 10, 18), 10, 100m, BookingStatus.Confirmed, transactionId: 9);

        var dashboard = await GetDashboardAsync(harness);

        dashboard.UpcomingBookings.Should().HaveCount(5, "the schedule is capped at five entries");
        dashboard.UpcomingBookings.Select(b => b.TransactionId)
            .Should().Equal(new[] { 3, 10, 1, 2, 4 }, "ordered by event date, then transaction id");
        dashboard.UpcomingBookings.Should().OnlyContain(b => b.EventDate >= harness.Today);
    }

    [Fact]
    public async Task An_upcoming_booking_should_carry_the_ledger_figures()
    {
        var harness = new DashboardHarness();
        harness.AddBooking(new DateOnly(2026, 10, 20), 100, 500m, BookingStatus.Confirmed);
        harness.AddPayment(1, 200_000m, October1);

        var dashboard = await GetDashboardAsync(harness);

        var upcoming = dashboard.UpcomingBookings.Single();
        upcoming.TransactionId.Should().Be(1);
        upcoming.BookingNumber.Should().StartWith("BK-");
        upcoming.CustomerName.Should().Be("Santos, Maria");
        upcoming.EventDate.Should().Be(new DateOnly(2026, 10, 20));
        upcoming.NumberOfHeads.Should().Be(100);
        upcoming.NetAmountDue.Should().Be(50_000m);
        upcoming.AmountPaid.Should().Be(200_000m);
        upcoming.Balance.Should().Be(0m, "a balance is never negative");
    }

    // ─────────────────────────── the benchmark (DEC-040) ───────────────────────────

    [Fact]
    public async Task The_benchmark_should_use_the_configured_value()
    {
        var harness = new DashboardHarness();
        harness.FakeBenchmarks.Current = new SalsCaterSuite.Application.Common.Models.FoodCostBenchmark(40.0m, IsConfigured: true);

        var dashboard = await GetDashboardAsync(harness);

        dashboard.Benchmarks.TargetFoodCostPercentage.Should().Be(40.0m);
        dashboard.Benchmarks.TargetMarginPercentage.Should().Be(60.0m, "100 − 40");
        dashboard.Benchmarks.IsConfigured.Should().BeTrue();
    }

    [Fact]
    public async Task The_benchmark_should_fall_back_to_the_default_and_report_that_it_is_not_configured()
    {
        var harness = new DashboardHarness();

        var dashboard = await GetDashboardAsync(harness);

        dashboard.Benchmarks.TargetFoodCostPercentage.Should().Be(35.0m, "the BR-07 conservative default");
        dashboard.Benchmarks.TargetMarginPercentage.Should().Be(65.0m);
        dashboard.Benchmarks.IsConfigured.Should().BeFalse();
    }

    // ─────────────────────────── the period itself ───────────────────────────

    [Fact]
    public async Task The_response_should_carry_the_resolved_period()
    {
        var harness = new DashboardHarness();

        var dashboard = await GetDashboardAsync(harness);

        dashboard.Period.Year.Should().Be(2026);
        dashboard.Period.Month.Should().Be(10);
        dashboard.Period.Label.Should().Be("October 2026");
        dashboard.Period.StartDate.Should().Be(new DateOnly(2026, 10, 1));
        dashboard.Period.EndDate.Should().Be(new DateOnly(2026, 10, 31));
        dashboard.GeneratedAt.Should().Be(DashboardHarness.ReferenceNow);
    }

    // ─────────────────────────── helpers ───────────────────────────

    /// <summary>
    /// The reference population:
    /// 1 Confirmed 100 × 500 = 50,000, unpaid
    /// 2 Confirmed 100 × 300 = 30,000, 10,000 paid (payment state is not a lifecycle status)
    /// 3 Confirmed 50 × 400 = 20,000, 20,000 paid
    /// 4 Pending 80 × 250 = 20,000, unpaid
    /// 5 Completed 60 × 600 = 36,000, unpaid
    /// 6 NoShow 40 × 100 = 4,000, unpaid
    /// </summary>
    private static void SeedCommittedTentativeAndOtherBookings(DashboardHarness harness)
    {
        harness.AddBooking(harness.PeriodStart, 100, 500m, BookingStatus.Confirmed, transactionId: 1);
        harness.AddBooking(harness.PeriodStart, 100, 300m, BookingStatus.Confirmed, transactionId: 2);
        harness.AddBooking(harness.PeriodStart, 50, 400m, BookingStatus.Confirmed, transactionId: 3);
        harness.AddBooking(harness.PeriodStart, 80, 250m, BookingStatus.Pending, transactionId: 4);
        harness.AddBooking(harness.PeriodStart, 60, 600m, BookingStatus.Completed, transactionId: 5);
        harness.AddBooking(harness.PeriodStart, 40, 100m, BookingStatus.NoShow, transactionId: 6);

        harness.AddPayment(2, 10_000m, October1);
        harness.AddPayment(3, 20_000m, October1);
    }

    private static async Task<DashboardResponse> GetDashboardAsync(DashboardHarness harness)
    {
        var result = await harness.GetAsync();

        result.Succeeded.Should().BeTrue();
        result.Data.Should().NotBeNull();

        return result.Data!;
    }
}
