using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using SalsCaterSuite.Domain.Enums;
using SalsCaterSuite.IntegrationTests.Infrastructure;

namespace SalsCaterSuite.IntegrationTests.Dashboard;

/// <summary>
/// The operations-dashboard endpoint against a real SQL Server (LocalDB) database seeded with
/// known data (REQ-DSH-01, spec §12.1, AC-B1, AC-B2, AC-B3, AC-B13).
/// <para>
/// The test owns a <b>dedicated</b> disposable database, so the aggregate — which is measured
/// over the whole book — is not polluted by the data of the other integration tests. Every
/// expected figure is derived from the seed table with plain arithmetic.
/// </para>
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public class DashboardEndpointTests : IClassFixture<DashboardSeedFixture>
{
    private readonly DashboardSeedFixture _fixture;

    public DashboardEndpointTests(DashboardSeedFixture fixture)
    {
        _fixture = fixture;
    }

    // ─────────────────────────── the transport contract ───────────────────────────

    [Fact]
    public async Task The_endpoint_should_require_authentication()
    {
        var client = _fixture.Database.CreateClient();

        var response = await client.GetAsync($"/api/dashboard?period={_fixture.Period}");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await response.ReadErrorsAsync()).Should().ContainSingle()
            .Which.Should().Be("Authentication is required.");
    }

    [Fact]
    public async Task A_malformed_period_should_be_rejected_with_the_contract_message()
    {
        var client = await _fixture.Database.CreateAdministratorClientAsync();

        var response = await client.GetAsync("/api/dashboard?period=banana");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.ReadErrorsAsync()).Should().ContainSingle()
            .Which.Should().Be("Period must be in the format YYYY-MM.");
    }

    [Fact]
    public async Task The_endpoint_should_return_the_full_payload_for_a_valid_period()
    {
        var client = await _fixture.Database.CreateAdministratorClientAsync();

        var response = await client.GetAsync($"/api/dashboard?period={_fixture.Period}");

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.DescribeAsync());

        var dashboard = await response.Content.ReadFromJsonAsync<JsonElement>();

        dashboard.GetProperty("period").GetProperty("year").GetInt32().Should().Be(_fixture.Today.Year);
        dashboard.GetProperty("period").GetProperty("month").GetInt32().Should().Be(_fixture.Today.Month);
        dashboard.GetProperty("period").GetProperty("startDate").GetString()
            .Should().Be(_fixture.PeriodStart.ToString("yyyy-MM-dd"));
        dashboard.GetProperty("period").GetProperty("endDate").GetString()
            .Should().Be(_fixture.PeriodEnd.ToString("yyyy-MM-dd"));
        dashboard.GetProperty("generatedAt").GetDateTimeOffset().Should().NotBe(default);
    }

    [Fact]
    public async Task The_endpoint_should_default_to_the_current_utc_calendar_month()
    {
        var client = await _fixture.Database.CreateAdministratorClientAsync();

        var explicitPeriod = await (await client.GetAsync($"/api/dashboard?period={_fixture.Period}"))
            .Content.ReadFromJsonAsync<JsonElement>();
        var defaultPeriod = await (await client.GetAsync("/api/dashboard"))
            .Content.ReadFromJsonAsync<JsonElement>();

        defaultPeriod.GetProperty("period").GetProperty("year").GetInt32()
            .Should().Be(explicitPeriod.GetProperty("period").GetProperty("year").GetInt32());
        defaultPeriod.GetProperty("period").GetProperty("month").GetInt32()
            .Should().Be(explicitPeriod.GetProperty("period").GetProperty("month").GetInt32());
    }

    // ─────────────────────────── the bookings KPI ───────────────────────────

    [Fact]
    public async Task The_bookings_kpi_should_split_the_period_population()
    {
        var dashboard = await GetDashboardAsync();

        var bookings = dashboard.GetProperty("bookings");

        bookings.GetProperty("totalCount").GetInt32().Should().Be(5, "A, B, C, D and E");
        bookings.GetProperty("committedCount").GetInt32().Should().Be(1, "only D is lifecycle Confirmed (DEC-177)");
        bookings.GetProperty("tentativeCount").GetInt32().Should().Be(3, "A, B and C stay Pending; a payment does not commit them");
        bookings.GetProperty("otherCount").GetInt32().Should().Be(1, "E");
        bookings.GetProperty("totalPax").GetInt32().Should().Be(390, "100 + 100 + 50 + 80 + 60");
    }

    [Fact]
    public async Task The_contracted_value_and_the_revenue_should_be_the_sum_of_the_net_amount_due()
    {
        var dashboard = await GetDashboardAsync();

        // 48,000 + 28,000 + 20,000 + 19,200 + 36,000 — each net already carries the automatic
        // headcount band of BR-38 (100, 100, 50, 80 and 60 heads).
        var expected = 151_200m;

        dashboard.GetProperty("bookings").GetProperty("contractedValue").GetDecimal().Should().Be(expected);
        dashboard.GetProperty("revenue").GetProperty("netContractedRevenue").GetDecimal().Should().Be(expected);
        dashboard.GetProperty("revenue").GetProperty("bookingCount").GetInt32().Should().Be(5);
        dashboard.GetProperty("revenue").GetProperty("averageBookingValue").GetDecimal()
            .Should().Be(30_240m, "151,200 ÷ 5");
    }

    // ─────────────────────────── the collections KPI ───────────────────────────

    [Fact]
    public async Task The_collections_should_be_the_payments_made_inside_the_period()
    {
        var dashboard = await GetDashboardAsync();

        var collections = dashboard.GetProperty("collections");

        collections.GetProperty("amount").GetDecimal().Should().Be(_fixture.PeriodPaymentTotal);
        collections.GetProperty("receiptCount").GetInt32().Should().Be(_fixture.PeriodPaymentCount);
        collections.GetProperty("previousPeriodAmount").GetDecimal()
            .Should().Be(_fixture.PreviousPeriodPaymentTotal);
        collections.GetProperty("changePercentage").GetDecimal()
            .Should().Be(200.00m, "(90,000 − 30,000) ÷ 30,000 × 100");
        collections.GetProperty("collectionRatioPercentage").GetDecimal()
            .Should().Be(59.52m, "90,000 ÷ 151,200 × 100");
    }

    // ─────────────────────────── the receivables KPI ───────────────────────────

    [Fact]
    public async Task The_receivables_should_cover_every_non_cancelled_booking()
    {
        var dashboard = await GetDashboardAsync();

        var receivables = dashboard.GetProperty("receivables");

        var expected = _fixture.ReceivableBookings.Sum(b => b.Balance);
        var expectedCount = _fixture.ReceivableBookings.Count(b => b.Balance > 0m);

        expected.Should().Be(204_200m, "the seeded truth, computed from the seed table");
        receivables.GetProperty("outstandingAmount").GetDecimal().Should().Be(expected);
        receivables.GetProperty("bookingCount").GetInt32().Should().Be(expectedCount);
    }

    [Fact]
    public async Task The_aging_buckets_should_sum_exactly_to_the_outstanding_receivables()
    {
        var dashboard = await GetDashboardAsync();

        var receivables = dashboard.GetProperty("receivables");
        var buckets = receivables.GetProperty("agingBuckets").EnumerateArray().ToList();

        buckets.Should().HaveCount(5);
        buckets.Select(b => b.GetProperty("key").GetString()).Should().Equal(
            "NotYetDue", "Days1To30", "Days31To60", "Days61To90", "Over90Days");

        buckets.Sum(b => b.GetProperty("amount").GetDecimal())
            .Should().Be(receivables.GetProperty("outstandingAmount").GetDecimal(),
                "the bucket amounts must reconcile to the cent");

        buckets.Sum(b => b.GetProperty("bookingCount").GetInt32())
            .Should().Be(receivables.GetProperty("bookingCount").GetInt32(),
                "every booking with a balance lands in exactly one bucket");

        // The buckets are derived from the seeded event dates and the due date (the event
        // date — DEC-041), with the boundaries of the specification's table.
        foreach (var bucket in buckets)
        {
            var key = bucket.GetProperty("key").GetString()!;
            var expected = ExpectedBucketAmount(key);

            bucket.GetProperty("amount").GetDecimal().Should().Be(expected, $"bucket {key}");
            bucket.GetProperty("bookingCount").GetInt32()
                .Should().Be(ExpectedBucketCount(key), $"bucket {key}");
        }

        receivables.GetProperty("overdueCount").GetInt32().Should().Be(4, "H, I, J and L");
        receivables.GetProperty("overdueAmount").GetDecimal().Should().Be(17_000m, "4,000 + 4,000 + 4,000 + 5,000");
    }

    // ─────────────────────────── the reconciliation (AC-B3) ───────────────────────────

    [Fact]
    public async Task The_receivables_should_reconcile_with_the_per_booking_ledger_to_the_cent()
    {
        var client = await _fixture.Database.CreateAdministratorClientAsync();

        var dashboard = await GetDashboardAsync();
        var outstanding = dashboard.GetProperty("receivables").GetProperty("outstandingAmount").GetDecimal();

        var ledgerBalances = new List<decimal>();

        foreach (var booking in _fixture.ReceivableBookings)
        {
            var ledger = await client.GetJsonAsync($"/api/bookings/{booking.TransactionId}/ledger");
            var balanceDue = ledger.GetProperty("balanceDue").GetDecimal();

            balanceDue.Should().Be(booking.Balance, $"booking {booking.Key}");
            ledgerBalances.Add(balanceDue);
        }

        ledgerBalances.Should().HaveCount(13, "every non-cancelled booking");
        ledgerBalances.Sum().Should().Be(
            outstanding,
            "the dashboard's AR must equal the sum of the per-booking ledger balances (DEC-025, AC-B3)");
    }

    // ─────────────────────────── the menu costing KPIs ───────────────────────────

    [Fact]
    public async Task The_dish_costing_coverage_should_measure_the_costed_share_of_the_catalogue()
    {
        var dashboard = await GetDashboardAsync();

        var dishCosting = dashboard.GetProperty("dishCosting");
        var total = _fixture.Menus.Count;
        var costed = _fixture.Menus.Count(m => m.IsCosted);

        dishCosting.GetProperty("totalMenuCount").GetInt32().Should().Be(total);
        dishCosting.GetProperty("costedMenuCount").GetInt32().Should().Be(costed);
        dishCosting.GetProperty("uncostedMenuCount").GetInt32().Should().Be(total - costed);
        dishCosting.GetProperty("coveragePercentage").GetDecimal()
            .Should().Be(66.67m, "4 ÷ 6 × 100");
    }

    [Fact]
    public async Task The_catalogue_margin_should_be_measured_over_the_costed_and_priced_menus()
    {
        var dashboard = await GetDashboardAsync();

        var margin = dashboard.GetProperty("margin");
        var population = _fixture.Menus.Where(m => m.IsInMarginPopulation).ToList();

        var revenue = population.Sum(m => m.Price ?? 0m);
        var foodCost = population.Sum(m => m.FoodCost ?? 0m);

        revenue.Should().Be(970m, "350 + 300 + 200 + 120");
        foodCost.Should().Be(315m, "125 + 90 + 60 + 40");

        margin.GetProperty("catalogueRevenueAmount").GetDecimal().Should().Be(revenue);
        margin.GetProperty("catalogueFoodCostAmount").GetDecimal().Should().Be(foodCost);
        margin.GetProperty("catalogueMarginAmount").GetDecimal().Should().Be(revenue - foodCost);
        margin.GetProperty("catalogueMarginPercentage").GetDecimal()
            .Should().Be(67.53m, "655 ÷ 970 × 100");
        margin.GetProperty("varianceToBenchmarkPercentage").GetDecimal()
            .Should().Be(2.53m, "67.53 − 65.00");
    }

    [Fact]
    public async Task The_food_cost_by_category_should_report_every_course_and_the_uncategorised_group()
    {
        var dashboard = await GetDashboardAsync();

        var categories = dashboard.GetProperty("foodCostByCategory").EnumerateArray().ToList();

        categories.Should().HaveCount(3);
        categories.Select(c => c.GetProperty("courseName").GetString()).Should().Equal(
            "Main Course", "Appetizer", "Uncategorised");

        categories[0].GetProperty("foodCostAmount").GetDecimal().Should().Be(215m, "125 + 90");
        categories[0].GetProperty("sharePercentage").GetDecimal().Should().Be(68.25m, "215 ÷ 315 × 100");
        categories[0].GetProperty("menuCount").GetInt32().Should().Be(2);

        categories[1].GetProperty("foodCostAmount").GetDecimal().Should().Be(60m);
        categories[1].GetProperty("sharePercentage").GetDecimal().Should().Be(19.05m, "60 ÷ 315 × 100");
        categories[1].GetProperty("menuCount").GetInt32().Should().Be(1);

        categories[2].GetProperty("courseId").ValueKind.Should().Be(JsonValueKind.Null);
        categories[2].GetProperty("courseName").GetString().Should().Be("Uncategorised");
        categories[2].GetProperty("foodCostAmount").GetDecimal().Should().Be(40m);
        categories[2].GetProperty("sharePercentage").GetDecimal().Should().Be(12.70m, "40 ÷ 315 × 100");
        categories[2].GetProperty("menuCount").GetInt32().Should().Be(1);
    }

    // ─────────────────────────── the ledger tiles ───────────────────────────

    [Fact]
    public async Task The_ledger_tiles_should_report_the_trailing_collections_and_the_awaiting_deposits()
    {
        var dashboard = await GetDashboardAsync();

        var ledger = dashboard.GetProperty("ledger");

        // Every payment the API recorded is dated "now", so it falls inside the trailing
        // seven days; the back-dated payment of the previous month does not.
        ledger.GetProperty("collectedLast7DaysAmount").GetDecimal().Should().Be(_fixture.PeriodPaymentTotal);
        ledger.GetProperty("collectedLast7DaysReceiptCount").GetInt32().Should().Be(_fixture.PeriodPaymentCount);
        ledger.GetProperty("receiptsIssuedCount").GetInt32().Should().Be(_fixture.PeriodPaymentCount);

        var awaiting = _fixture.ReceivableBookings.Where(b => b.AmountPaid == 0m).ToList();
        ledger.GetProperty("awaitingDepositBookingCount").GetInt32().Should().Be(awaiting.Count);
        ledger.GetProperty("awaitingDepositAmount").GetDecimal()
            .Should().Be(awaiting.Sum(b => b.NetAmountDue));
    }

    // ─────────────────────────── the upcoming schedule ───────────────────────────

    [Fact]
    public async Task The_upcoming_bookings_should_be_the_future_events_ordered_by_event_date()
    {
        var dashboard = await GetDashboardAsync();

        var upcoming = dashboard.GetProperty("upcomingBookings").EnumerateArray().ToList();

        var expected = _fixture.Bookings
            .Where(b => b.EventDate >= _fixture.Today
                        && b.Status is not (BookingStatus.Cancelled or BookingStatus.Expired))
            .OrderBy(b => b.EventDate)
            .ThenBy(b => b.TransactionId)
            .Take(5)
            .ToList();

        upcoming.Should().HaveCount(expected.Count);
        upcoming.Select(u => u.GetProperty("transactionId").GetInt32())
            .Should().Equal(expected.Select(b => b.TransactionId));

        foreach (var booking in expected)
        {
            var entry = upcoming.Single(u => u.GetProperty("transactionId").GetInt32() == booking.TransactionId);

            entry.GetProperty("netAmountDue").GetDecimal().Should().Be(booking.NetAmountDue);
            entry.GetProperty("amountPaid").GetDecimal().Should().Be(booking.AmountPaid);
            entry.GetProperty("balance").GetDecimal().Should().Be(booking.Balance);
            entry.GetProperty("eventDate").GetString().Should().Be(booking.EventDate.ToString("yyyy-MM-dd"));
            entry.GetProperty("numberOfHeads").GetInt32().Should().Be(booking.Heads);
            entry.GetProperty("status").GetString().Should().Be(booking.Status.ToString());
            entry.GetProperty("customerName").GetString().Should().Be("Dashboard, Integration");
        }
    }

    // ─────────────────────────── the benchmark ───────────────────────────

    [Fact]
    public async Task The_benchmark_should_come_from_the_configuration()
    {
        var dashboard = await GetDashboardAsync();

        var benchmarks = dashboard.GetProperty("benchmarks");

        benchmarks.GetProperty("targetFoodCostPercentage").GetDecimal().Should().Be(35.0m);
        benchmarks.GetProperty("targetMarginPercentage").GetDecimal().Should().Be(65.0m);
        benchmarks.GetProperty("isConfigured").GetBoolean().Should().BeTrue();
    }

    // ─────────────────────────── helpers ───────────────────────────

    private async Task<JsonElement> GetDashboardAsync()
    {
        var client = await _fixture.Database.CreateAdministratorClientAsync();

        var response = await client.GetAsync($"/api/dashboard?period={_fixture.Period}");

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.DescribeAsync());

        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    /// <summary>
    /// The aging bucket of a booking, derived from the specification's table (spec §6.7):
    /// the due date is the event date (DEC-041).
    /// </summary>
    private decimal ExpectedBucketAmount(string key)
    {
        var today = _fixture.Today;

        return _fixture.ReceivableBookings
            .Where(b => b.Balance > 0m)
            .GroupBy(b => BucketOf(b.EventDate, today))
            .Where(g => g.Key == key)
            .Select(g => g.Sum(b => b.Balance))
            .DefaultIfEmpty(0m)
            .First();
    }

    private int ExpectedBucketCount(string key)
    {
        var today = _fixture.Today;

        return _fixture.ReceivableBookings
            .Where(b => b.Balance > 0m)
            .GroupBy(b => BucketOf(b.EventDate, today))
            .Where(g => g.Key == key)
            .Select(g => g.Count())
            .DefaultIfEmpty(0)
            .First();
    }

    private static string BucketOf(DateOnly eventDate, DateOnly today)
    {
        if (eventDate >= today)
        {
            return "NotYetDue";
        }

        var daysOverdue = today.DayNumber - eventDate.DayNumber;

        if (daysOverdue <= 30) return "Days1To30";
        if (daysOverdue <= 60) return "Days31To60";
        if (daysOverdue <= 90) return "Days61To90";

        return "Over90Days";
    }
}
