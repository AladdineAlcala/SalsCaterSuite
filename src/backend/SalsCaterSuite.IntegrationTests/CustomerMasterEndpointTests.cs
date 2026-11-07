using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using SalsCaterSuite.IntegrationTests.Infrastructure;

namespace SalsCaterSuite.IntegrationTests;

/// <summary>
/// The Customer Master endpoints against a real SQL Server (LocalDB) database
/// (spec §5.1 / §5.4 / §5.5, REQ-CUS-01).
/// <para>
/// The tests own a distinctive customer name so the assertions are unaffected by the data
/// the other integration tests create in the same database.
/// </para>
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public class CustomerMasterEndpointTests
{
    /// <summary>The distinctive last name of the customer this test class owns.</summary>
    private const string CustomerLastName = "Customermasterprobe";

    private readonly IntegrationDatabaseFixture _fixture;

    public CustomerMasterEndpointTests(IntegrationDatabaseFixture fixture) => _fixture = fixture;

    // ─────────────────────────── the directory endpoint ───────────────────────────

    [Fact]
    public async Task The_directory_endpoint_should_return_the_paged_payload()
    {
        var client = await _fixture.CreateAdministratorClientAsync();
        var customerId = await CreateProbeCustomerAsync(client, "A");

        // A booking that is 45 days past its event date and only half paid.
        var booking = await client.CreateBookingAsync(customerId, 100, 500m, DaysAgo(45));
        (await client.RecordPaymentAsync(booking, 25_000m)).StatusCode.Should().Be(HttpStatusCode.OK);

        var response = await client.GetAsync($"/api/customers/directory?search={CustomerLastName}A");
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.DescribeAsync());

        var directory = await response.Content.ReadFromJsonAsync<JsonElement>();

        directory.GetProperty("page").GetInt32().Should().Be(1);
        directory.GetProperty("pageSize").GetInt32().Should().Be(10);
        directory.GetProperty("totalCount").GetInt32().Should().Be(1);
        directory.GetProperty("totalPages").GetInt32().Should().Be(1);

        var item = directory.GetProperty("items").EnumerateArray().Single();
        item.GetProperty("customerId").GetInt32().Should().Be(customerId);
        item.GetProperty("customerType").GetString().Should().Be("Corporate");
        // 100 heads x 500.00 less the automatic 100-999 band of 20.00 per head (BR-38).
        item.GetProperty("totalContractedValue").GetDecimal().Should().Be(TestApi.NetAmountDueFor(100, 500m));
        item.GetProperty("totalPaid").GetDecimal().Should().Be(25_000m);
        item.GetProperty("outstandingBalance").GetDecimal().Should().Be(TestApi.NetAmountDueFor(100, 500m) - 25_000m);
        item.GetProperty("ledgerState").GetString().Should().Be("Days31To60");
        item.GetProperty("ledgerStateLabel").GetString().Should().Be("Overdue 31–60 days");

        var counts = item.GetProperty("bookingCounts");
        counts.GetProperty("total").GetInt32().Should().Be(1);
        counts.GetProperty("past").GetInt32().Should().Be(1);
        counts.GetProperty("upcoming").GetInt32().Should().Be(0);

        var summary = directory.GetProperty("summary");
        summary.GetProperty("totalClients").GetInt32().Should().Be(1);
        summary.GetProperty("activeArAmount").GetDecimal().Should().Be(TestApi.NetAmountDueFor(100, 500m) - 25_000m);
        summary.GetProperty("clientsWithBalance").GetInt32().Should().Be(1);
        summary.GetProperty("totalContractedValue").GetDecimal().Should().Be(TestApi.NetAmountDueFor(100, 500m));
        summary.GetProperty("averageValuePerClient").GetDecimal().Should().Be(TestApi.NetAmountDueFor(100, 500m));
        summary.GetProperty("overdueAmount").GetDecimal()
            .Should().Be(TestApi.NetAmountDueFor(100, 500m) - 25_000m, "the booking is 45 days overdue");
        summary.GetProperty("overdueClientCount").GetInt32().Should().Be(1);
    }

    /// <summary>
    /// The quick-view pills describe the search-filtered population, so selecting a scope
    /// narrows the summary but never the pills (spec §4.3, contract §10.1).
    /// </summary>
    [Fact]
    public async Task The_quick_view_counts_should_describe_the_search_filtered_population()
    {
        var client = await _fixture.CreateAdministratorClientAsync();

        // Two customers behind one search term: a corporate one with an open balance and a
        // private one who is settled.
        var corporateId = await CreateProbeCustomerAsync(client, "Q1");
        await CreateProbeCustomerAsync(client, "Q2", company: null);

        var booking = await client.CreateBookingAsync(corporateId, 100, 500m, DaysAgo(45));
        (await client.RecordPaymentAsync(booking, 25_000m)).StatusCode.Should().Be(HttpStatusCode.OK);

        var search = $"{CustomerLastName}Q";

        var unfiltered = await client.GetJsonAsync($"/api/customers/directory?search={search}");
        var corporate = await client.GetJsonAsync($"/api/customers/directory?search={search}&customerType=Corporate");
        var overdue30 = await client.GetJsonAsync($"/api/customers/directory?search={search}&arState=Overdue30");

        var unfilteredCounts = unfiltered.GetProperty("quickViewCounts");
        var corporateCounts = corporate.GetProperty("quickViewCounts");
        var overdueCounts = overdue30.GetProperty("quickViewCounts");

        // The population behind the search term.
        unfilteredCounts.GetProperty("all").GetInt32().Should().Be(2);
        unfilteredCounts.GetProperty("corporate").GetInt32().Should().Be(1);
        unfilteredCounts.GetProperty("private").GetInt32().Should().Be(1);
        unfilteredCounts.GetProperty("withOpenBalance").GetInt32().Should().Be(1);
        unfilteredCounts.GetProperty("overdue30").GetInt32().Should().Be(1);

        // corporate + private = all.
        (unfilteredCounts.GetProperty("corporate").GetInt32()
            + unfilteredCounts.GetProperty("private").GetInt32())
            .Should().Be(unfilteredCounts.GetProperty("all").GetInt32());

        // The summary follows the active scope…
        corporate.GetProperty("summary").GetProperty("totalClients").GetInt32().Should().Be(1);
        overdue30.GetProperty("summary").GetProperty("totalClients").GetInt32().Should().Be(1);

        // …the pills keep describing the population.
        corporateCounts.GetProperty("all").GetInt32().Should().Be(2);
        overdueCounts.GetProperty("all").GetInt32().Should().Be(2);
    }

    /// <summary>The aggregate carries the server's UTC instant (spec §4.10).</summary>
    [Fact]
    public async Task The_directory_endpoint_should_carry_the_server_generated_timestamp()
    {
        var client = await _fixture.CreateAdministratorClientAsync();

        var before = DateTimeOffset.UtcNow;
        var directory = await client.GetJsonAsync("/api/customers/directory?pageSize=1");
        var after = DateTimeOffset.UtcNow;

        var generatedAt = directory.GetProperty("generatedAt").GetDateTimeOffset();

        generatedAt.Offset.Should().Be(TimeSpan.Zero, "the server reports a UTC instant");
        generatedAt.Should().BeOnOrAfter(before.AddSeconds(-5)).And.BeOnOrBefore(after.AddSeconds(5));
    }

    [Fact]
    public async Task The_directory_endpoint_should_clamp_the_paging_and_ignore_an_unknown_filter()
    {
        var client = await _fixture.CreateAdministratorClientAsync();

        // page = 0 and pageSize = 1000 are clamped, never rejected.
        var clamped = await client.GetAsync("/api/customers/directory?page=0&pageSize=1000");
        clamped.StatusCode.Should().Be(HttpStatusCode.OK, await clamped.DescribeAsync());

        var directory = await clamped.Content.ReadFromJsonAsync<JsonElement>();
        directory.GetProperty("page").GetInt32().Should().Be(1);
        directory.GetProperty("pageSize").GetInt32().Should().Be(100);

        // An unknown customer type / AR state is ignored, not a 400.
        var ignored = await client.GetAsync("/api/customers/directory?customerType=banana&arState=banana");
        ignored.StatusCode.Should().Be(HttpStatusCode.OK, await ignored.DescribeAsync());
    }

    [Fact]
    public async Task The_directory_summary_should_reconcile_with_the_per_customer_balances()
    {
        var client = await _fixture.CreateAdministratorClientAsync();

        var response = await client.GetAsync("/api/customers/directory?pageSize=100");
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.DescribeAsync());

        var directory = await response.Content.ReadFromJsonAsync<JsonElement>();

        var balances = directory.GetProperty("items").EnumerateArray()
            .Sum(i => i.GetProperty("outstandingBalance").GetDecimal());

        balances.Should().Be(
            directory.GetProperty("summary").GetProperty("activeArAmount").GetDecimal(),
            "the summary's active AR equals the sum of the per-customer balances (DEC-025, AC-B3)");
    }

    // ─────────────────────────── the SOA PDF endpoint (B10) ───────────────────────────

    [Fact]
    public async Task The_soa_endpoint_should_return_a_pdf_for_an_existing_customer()
    {
        var client = await _fixture.CreateAdministratorClientAsync();
        var customerId = await CreateProbeCustomerAsync(client, "B");

        var response = await client.GetAsync($"/api/customers/{customerId}/soa");

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.DescribeAsync());
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/pdf");

        var disposition = response.Content.Headers.ContentDisposition;
        disposition.Should().NotBeNull();
        disposition!.FileName.Should().Contain($"SOA-{customerId}-");

        var bytes = await response.Content.ReadAsByteArrayAsync();
        bytes.Should().NotBeEmpty();
        Encoding.ASCII.GetString(bytes, 0, 4).Should().Be("%PDF", "the response is a real PDF document");
    }

    [Fact]
    public async Task The_soa_endpoint_should_return_not_found_for_an_unknown_customer()
    {
        var client = await _fixture.CreateAdministratorClientAsync();

        var response = await client.GetAsync("/api/customers/99999999/soa");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await response.ReadErrorsAsync()).Should().ContainSingle();
    }

    // ─────────────────────────── the directory export (B11) ───────────────────────────

    [Fact]
    public async Task The_export_endpoint_should_return_an_xlsx_of_the_filtered_result_set()
    {
        var client = await _fixture.CreateAdministratorClientAsync();
        var customerId = await CreateProbeCustomerAsync(client, "C");

        var response = await client.GetAsync($"/api/customers/directory/export?search={CustomerLastName}C");

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.DescribeAsync());
        response.Content.Headers.ContentType!.MediaType.Should()
            .Be("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");

        var disposition = response.Content.Headers.ContentDisposition;
        disposition.Should().NotBeNull();
        disposition!.FileName.Should().Contain("CustomerDirectory-");

        var bytes = await response.Content.ReadAsByteArrayAsync();
        bytes.Should().NotBeEmpty();
        Encoding.ASCII.GetString(bytes, 0, 2).Should().Be("PK", "an XLSX file is a zip archive");
    }

    // ─────────────────────────── the corrected statement ───────────────────────────

    [Fact]
    public async Task The_statement_endpoint_should_report_the_net_amount_due_and_the_lifecycle_status()
    {
        var client = await _fixture.CreateAdministratorClientAsync();
        var customerId = await CreateProbeCustomerAsync(client, "D");

        var booking = await client.CreateBookingAsync(customerId, 100, 500m, DaysAgo(45));
        (await client.RecordPaymentAsync(booking, 25_000m)).StatusCode.Should().Be(HttpStatusCode.OK);

        var statement = await client.GetJsonAsync($"/api/customers/{customerId}/statement");

        statement.GetProperty("totalBilled").GetDecimal().Should().Be(TestApi.NetAmountDueFor(100, 500m));
        statement.GetProperty("totalPaid").GetDecimal().Should().Be(25_000m);
        statement.GetProperty("outstandingBalance").GetDecimal()
            .Should().Be(TestApi.NetAmountDueFor(100, 500m) - 25_000m);
        statement.GetProperty("totalRefunded").GetDecimal().Should().Be(0m);
        statement.GetProperty("ledgerState").GetString().Should().Be("Days31To60");
        statement.GetProperty("ledgerStateLabel").GetString().Should().Be("Overdue 31–60 days");

        var line = statement.GetProperty("transactions").EnumerateArray().Single();
        line.GetProperty("amount").GetDecimal()
            .Should().Be(TestApi.NetAmountDueFor(100, 500m), "the statement uses the ledger's net amount due");
        line.GetProperty("balance").GetDecimal().Should().Be(TestApi.NetAmountDueFor(100, 500m) - 25_000m);
        line.GetProperty("status").GetString().Should().BeOneOf(
            "Pending", "Confirmed", "InProgress",
            "Completed", "Cancelled", "NoShow", "Rescheduled", "Expired");
        line.GetProperty("agingBucket").GetString().Should().Be("Days31To60");
        line.GetProperty("bookingNumber").GetString().Should().StartWith("BK-");
    }

    // ─────────────────────────── the PUT validation (B7) ───────────────────────────

    [Fact]
    public async Task The_update_endpoint_should_reject_a_blank_name_with_the_errors_contract()
    {
        var client = await _fixture.CreateAdministratorClientAsync();
        var customerId = await CreateProbeCustomerAsync(client, "E");

        var response = await client.PutAsJsonAsync(
            $"/api/customers/{customerId}",
            new CustomerPayload(" ", "Probe", Company: "Probe Catering"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.ReadErrorsAsync()).Should().Contain("Last name is required.");

        // Nothing was changed.
        var customer = await client.GetJsonAsync($"/api/customers/{customerId}");
        customer.GetProperty("lastName").GetString().Should().Be($"{CustomerLastName}E");
    }

    [Fact]
    public async Task The_update_endpoint_should_reject_an_over_length_name_with_the_errors_contract()
    {
        var client = await _fixture.CreateAdministratorClientAsync();
        var customerId = await CreateProbeCustomerAsync(client, "F");

        var response = await client.PutAsJsonAsync(
            $"/api/customers/{customerId}",
            new CustomerPayload(new string('x', 200), "Probe", Company: "Probe Catering"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.ReadErrorsAsync()).Should().Contain("Last name must not exceed 100 characters.");
    }

    // ─────────────────────────── the single-customer endpoint ───────────────────────────

    [Fact]
    public async Task The_customer_endpoint_should_return_the_extended_profile()
    {
        var client = await _fixture.CreateAdministratorClientAsync();
        var customerId = await CreateProbeCustomerAsync(client, "G");

        var eventDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(20);
        var booking = await client.CreateBookingAsync(customerId, 100, 500m, eventDate);
        (await client.RecordPaymentAsync(booking, 25_000m)).StatusCode.Should().Be(HttpStatusCode.OK);

        var customer = await client.GetJsonAsync($"/api/customers/{customerId}");

        customer.GetProperty("customerType").GetString().Should().Be("Corporate");
        customer.GetProperty("totalContractedValue").GetDecimal().Should().Be(TestApi.NetAmountDueFor(100, 500m));
        customer.GetProperty("totalPaid").GetDecimal().Should().Be(25_000m);
        customer.GetProperty("outstandingBalance").GetDecimal()
            .Should().Be(TestApi.NetAmountDueFor(100, 500m) - 25_000m);
        customer.GetProperty("ledgerState").GetString().Should().Be("NotYetDue");
        customer.GetProperty("ledgerStateLabel").GetString().Should().Be("Open · not yet due");

        var upcoming = customer.GetProperty("upcomingBooking");
        upcoming.GetProperty("transactionId").GetInt32().Should().Be(booking);
        upcoming.GetProperty("bookingNumber").GetString().Should().StartWith("BK-");
        upcoming.GetProperty("eventDate").GetString().Should().Be(eventDate.ToString("yyyy-MM-dd"));
        upcoming.GetProperty("amountDue").GetDecimal().Should().Be(TestApi.NetAmountDueFor(100, 500m));
    }

    // ─────────────────────────── helpers ───────────────────────────

    /// <summary>
    /// Creates the customer this test owns. Every test uses its own suffix so the
    /// directory searches never see another test's probe customer.
    /// <para>
    /// The company name makes the derived classification <c>Corporate</c>; pass
    /// <c>company: null</c> for a <c>Private</c> customer.
    /// </para>
    /// </summary>
    private static async Task<int> CreateProbeCustomerAsync(
        HttpClient client,
        string suffix,
        string? company = "Probe Catering")
    {
        var response = await client.PostAsJsonAsync(
            "/api/customers",
            new CustomerPayload($"{CustomerLastName}{suffix}", "Probe", Company: company));

        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.DescribeAsync());

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("customerId").GetInt32();
    }

    private static DateOnly DaysAgo(int days) => DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-days);
}
