using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using SalsCaterSuite.IntegrationTests.Infrastructure;

namespace SalsCaterSuite.IntegrationTests.Dashboard;

/// <summary>
/// AC-B9 — soft-deleted bookings, payments and menus are excluded from every dashboard
/// figure, and the exclusion is dashboard-scoped: no global query filter is introduced
/// (DEC-038, spec §6.13).
/// <para>
/// The test owns a dedicated disposable database so the aggregate is not polluted by the data
/// of the other integration tests.
/// </para>
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public class DashboardSoftDeleteExclusionTests : IClassFixture<DashboardSoftDeleteSeedFixture>
{
    private readonly DashboardSoftDeleteSeedFixture _fixture;

    public DashboardSoftDeleteExclusionTests(DashboardSoftDeleteSeedFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task A_soft_deleted_booking_should_be_excluded_from_the_population_and_the_receivables()
    {
        var dashboard = await GetDashboardAsync();

        var bookings = dashboard.GetProperty("bookings");
        bookings.GetProperty("totalCount").GetInt32().Should().Be(2, "the soft-deleted booking is not in the population");
        bookings.GetProperty("contractedValue").GetDecimal().Should().Be(96_000m, "48,000 + 48,000");
        dashboard.GetProperty("revenue").GetProperty("netContractedRevenue").GetDecimal().Should().Be(96_000m);

        var receivables = dashboard.GetProperty("receivables");
        receivables.GetProperty("outstandingAmount").GetDecimal()
            .Should().Be(76_000m, "28,000 (live booking) + 48,000 (booking whose payment is soft-deleted)");
        receivables.GetProperty("bookingCount").GetInt32().Should().Be(2);
    }

    [Fact]
    public async Task A_soft_deleted_payment_should_be_excluded_from_the_collections_and_the_amount_paid()
    {
        var dashboard = await GetDashboardAsync();

        dashboard.GetProperty("collections").GetProperty("amount").GetDecimal()
            .Should().Be(20_000m, "only the live payment counts");
        dashboard.GetProperty("collections").GetProperty("receiptCount").GetInt32().Should().Be(1);
        dashboard.GetProperty("ledger").GetProperty("collectedLast7DaysAmount").GetDecimal()
            .Should().Be(20_000m);
        dashboard.GetProperty("ledger").GetProperty("collectedLast7DaysReceiptCount").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task A_soft_deleted_menu_should_be_excluded_from_the_coverage_and_the_margin()
    {
        var dashboard = await GetDashboardAsync();

        var dishCosting = dashboard.GetProperty("dishCosting");
        dishCosting.GetProperty("totalMenuCount").GetInt32().Should().Be(2, "the soft-deleted menu is excluded");
        dishCosting.GetProperty("costedMenuCount").GetInt32().Should().Be(1);
        dishCosting.GetProperty("uncostedMenuCount").GetInt32().Should().Be(1);
        dishCosting.GetProperty("coveragePercentage").GetDecimal().Should().Be(50.00m, "1 ÷ 2 × 100");

        var margin = dashboard.GetProperty("margin");
        margin.GetProperty("catalogueRevenueAmount").GetDecimal().Should().Be(350m, "the soft-deleted menu's 300 is excluded");
        margin.GetProperty("catalogueFoodCostAmount").GetDecimal().Should().Be(125m);
        margin.GetProperty("catalogueMarginAmount").GetDecimal().Should().Be(225m);
        margin.GetProperty("catalogueMarginPercentage").GetDecimal().Should().Be(64.29m, "225 ÷ 350 × 100");
    }

    private async Task<JsonElement> GetDashboardAsync()
    {
        var client = await _fixture.Database.CreateAdministratorClientAsync();

        var response = await client.GetAsync($"/api/dashboard?period={_fixture.Period}");

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.DescribeAsync());

        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }
}
