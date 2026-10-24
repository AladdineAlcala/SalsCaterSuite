using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SalsCaterSuite.IntegrationTests.Infrastructure;

namespace SalsCaterSuite.IntegrationTests;

/// <summary>
/// The Packages &amp; Buffets catalogue endpoints against a real SQL Server (LocalDB) database
/// (spec §3.2 / §3.3, REQ-PKG-01).
/// <para>
/// The <c>Package</c> table starts empty (BR-13 forbids seeding business data), so every test
/// creates the tiers it needs through the real API and scopes its assertions to its own
/// uniquely-suffixed probe tiers.
/// </para>
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public class PackageCatalogueEndpointTests
{
    /// <summary>The distinctive prefix of the tier types this test class owns.</summary>
    private const string TierTypePrefix = "PCT";

    private readonly IntegrationDatabaseFixture _fixture;

    public PackageCatalogueEndpointTests(IntegrationDatabaseFixture fixture) => _fixture = fixture;

    // ─────────────────────────── the catalogue aggregate ───────────────────────────

    [Fact]
    public async Task The_summary_endpoint_should_return_the_documented_envelope()
    {
        var client = await _fixture.CreateAdministratorClientAsync();

        await CreateProbeTierAsync(client, "SUM", 950m, 80);

        var response = await client.GetAsync("/api/packages/summary");
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.DescribeAsync());

        var summary = await response.Content.ReadFromJsonAsync<JsonElement>();

        // The envelope of the specification (spec §3.2).
        summary.GetProperty("totalPackages").GetInt32().Should().BeGreaterThan(0);
        summary.GetProperty("activePackages").GetInt32().Should().BeGreaterThan(0);
        summary.GetProperty("inactivePackages").GetInt32().Should().BeGreaterThanOrEqualTo(0);
        summary.GetProperty("pricedPackages").GetInt32().Should().BeGreaterThan(0);
        summary.GetProperty("averageAmountPax").GetDecimal().Should().BeGreaterThan(0m);
        summary.GetProperty("lowestAmountPax").GetDecimal().Should().BeGreaterThan(0m);
        summary.GetProperty("highestAmountPax").GetDecimal().Should().BeGreaterThan(0m);
        summary.GetProperty("lowestMinPax").GetInt32().Should().BeGreaterThan(0);
        summary.GetProperty("highestMinPax").GetInt32().Should().BeGreaterThan(0);
        summary.GetProperty("generatedAt").GetDateTimeOffset().Offset.Should().Be(TimeSpan.Zero);
    }

    /// <summary>
    /// The aggregate must describe the very catalogue the list endpoint publishes — the same
    /// population, counted and measured the same way. A divergence here would let the KPI
    /// ribbon and the tier list describe two different catalogues.
    /// </summary>
    [Fact]
    public async Task The_summary_endpoint_should_reconcile_with_the_tier_list()
    {
        var client = await _fixture.CreateAdministratorClientAsync();

        await CreateProbeTierAsync(client, "REC", 1200m, 100);
        await CreateProbeTierAsync(client, "REC2", 1450m, 150);

        var tiers = await client.GetJsonAsync("/api/packages");
        var summary = await client.GetJsonAsync("/api/packages/summary");

        var all = tiers.EnumerateArray().ToList();
        var priced = all
            .Where(t => t.GetProperty("amountPax").ValueKind != JsonValueKind.Null)
            .Select(t => t.GetProperty("amountPax").GetDecimal())
            .ToList();
        var floors = all
            .Where(t => t.GetProperty("minPax").ValueKind != JsonValueKind.Null)
            .Select(t => t.GetProperty("minPax").GetInt32())
            .ToList();

        summary.GetProperty("totalPackages").GetInt32().Should().Be(all.Count);
        summary.GetProperty("activePackages").GetInt32()
            .Should().Be(all.Count(t => t.GetProperty("isActive").GetBoolean()));
        summary.GetProperty("inactivePackages").GetInt32()
            .Should().Be(all.Count(t => !t.GetProperty("isActive").GetBoolean()));
        summary.GetProperty("pricedPackages").GetInt32().Should().Be(priced.Count);

        summary.GetProperty("lowestAmountPax").GetDecimal().Should().Be(priced.Min());
        summary.GetProperty("highestAmountPax").GetDecimal().Should().Be(priced.Max());
        summary.GetProperty("averageAmountPax").GetDecimal()
            .Should().Be(Math.Round(priced.Sum() / priced.Count, 2, MidpointRounding.AwayFromZero));

        summary.GetProperty("lowestMinPax").GetInt32().Should().Be(floors.Min());
        summary.GetProperty("highestMinPax").GetInt32().Should().Be(floors.Max());
    }

    [Fact]
    public async Task The_summary_endpoint_should_count_the_new_tier()
    {
        var client = await _fixture.CreateAdministratorClientAsync();

        var before = await client.GetJsonAsync("/api/packages/summary");

        await CreateProbeTierAsync(client, "CNT", 777.77m, 50);

        var after = await client.GetJsonAsync("/api/packages/summary");

        after.GetProperty("totalPackages").GetInt32()
            .Should().Be(before.GetProperty("totalPackages").GetInt32() + 1);
        after.GetProperty("activePackages").GetInt32()
            .Should().Be(before.GetProperty("activePackages").GetInt32() + 1);
        after.GetProperty("inactivePackages").GetInt32()
            .Should().Be(before.GetProperty("inactivePackages").GetInt32());
        after.GetProperty("pricedPackages").GetInt32()
            .Should().Be(before.GetProperty("pricedPackages").GetInt32() + 1);

        // The probe's own rate is inside the published range.
        var lowest = after.GetProperty("lowestAmountPax").GetDecimal();
        var highest = after.GetProperty("highestAmountPax").GetDecimal();
        lowest.Should().BeLessThanOrEqualTo(777.77m);
        highest.Should().BeGreaterThanOrEqualTo(777.77m);
    }

    // ─────────────────────────── the write validation (BR-11, DEC-069) ───────────────────────────

    [Fact]
    public async Task The_create_endpoint_should_reject_a_blank_description_and_persist_nothing()
    {
        var client = await _fixture.CreateAdministratorClientAsync();
        var probeType = $"{TierTypePrefix}BAD";

        var response = await client.PostAsJsonAsync(
            "/api/packages",
            new PackagePayload(probeType, "   ", 100m, 10));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.ReadErrorsAsync()).Should().Contain("Package description is required.");

        // Nothing was persisted.
        var persisted = await _fixture.QueryAsync(context =>
            context.Packages.CountAsync(p => p.PackageType == probeType));

        persisted.Should().Be(0);
    }

    [Fact]
    public async Task The_create_endpoint_should_reject_a_negative_rate()
    {
        var client = await _fixture.CreateAdministratorClientAsync();
        var probeType = $"{TierTypePrefix}NEG";

        var response = await client.PostAsJsonAsync(
            "/api/packages",
            new PackagePayload(probeType, "Negative rate", -1m, 10));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.ReadErrorsAsync()).Should().Contain("Rate per guest must be zero or greater.");

        var persisted = await _fixture.QueryAsync(context =>
            context.Packages.CountAsync(p => p.PackageType == probeType));

        persisted.Should().Be(0);
    }

    [Fact]
    public async Task The_create_endpoint_should_return_created_and_the_new_tier_should_be_counted()
    {
        var client = await _fixture.CreateAdministratorClientAsync();

        var response = await client.PostAsJsonAsync(
            "/api/packages",
            new PackagePayload($"{TierTypePrefix}OK", "Probe tier OK", 1200m, 100));

        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.DescribeAsync());

        var created = await response.Content.ReadFromJsonAsync<JsonElement>();
        var packageId = created.GetProperty("packageId").GetInt32();
        packageId.Should().BeGreaterThan(0);

        // The new tier is in the catalogue…
        var tier = await client.GetJsonAsync($"/api/packages/{packageId}");
        tier.GetProperty("packageType").GetString().Should().Be($"{TierTypePrefix}OK");
        tier.GetProperty("amountPax").GetDecimal().Should().Be(1200m);

        // …and the summary counts it.
        var summary = await client.GetJsonAsync("/api/packages/summary");
        summary.GetProperty("totalPackages").GetInt32().Should().BeGreaterThan(0);
        summary.GetProperty("pricedPackages").GetInt32().Should().BeGreaterThan(0);
        summary.GetProperty("highestAmountPax").GetDecimal().Should().BeGreaterThanOrEqualTo(1200m);
    }

    [Fact]
    public async Task The_update_endpoint_should_return_not_found_for_an_unknown_tier()
    {
        var client = await _fixture.CreateAdministratorClientAsync();

        var response = await client.PutAsJsonAsync(
            "/api/packages/99999999",
            new PackagePayload($"{TierTypePrefix}UPD", "Unknown tier", 100m, 10));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await response.ReadErrorsAsync()).Should().ContainSingle();
    }

    [Fact]
    public async Task The_update_endpoint_should_return_bad_request_for_a_rejected_payload()
    {
        var client = await _fixture.CreateAdministratorClientAsync();

        var packageId = await CreateProbeTierAsync(client, "UPD2", 100m, 10);

        var response = await client.PutAsJsonAsync(
            $"/api/packages/{packageId}",
            new PackagePayload($"{TierTypePrefix}UPD2", string.Empty, -5m, 10));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var errors = await response.ReadErrorsAsync();
        errors.Should().Contain("Package description is required.");
        errors.Should().Contain("Rate per guest must be zero or greater.");

        // The stored tier is untouched.
        var tier = await client.GetJsonAsync($"/api/packages/{packageId}");
        tier.GetProperty("description").GetString().Should().Be("Probe tier UPD2");
        tier.GetProperty("amountPax").GetDecimal().Should().Be(100m);
    }

    [Fact]
    public async Task The_create_endpoint_should_generate_the_package_code_and_store_the_extended_amount()
    {
        var client = await _fixture.CreateAdministratorClientAsync();

        var response = await client.PostAsJsonAsync(
            "/api/packages",
            new PackagePayload("Premier", "Probe Premier extended", 1250m, 100, 150m));

        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.DescribeAsync());

        var created = await response.Content.ReadFromJsonAsync<JsonElement>();
        var packageId = created.GetProperty("packageId").GetInt32();

        var tier = await client.GetJsonAsync($"/api/packages/{packageId}");
        tier.GetProperty("packageCode").GetString().Should().MatchRegex("^PKG-PRE-\\d{2,}$");
        tier.GetProperty("extendedLocationAmount").GetDecimal().Should().Be(150m);
    }

    [Fact]
    public async Task The_create_endpoint_should_reject_an_extended_amount_on_an_ineligible_type()
    {
        var client = await _fixture.CreateAdministratorClientAsync();
        var probeType = $"{TierTypePrefix}EXT";

        var response = await client.PostAsJsonAsync(
            "/api/packages",
            new PackagePayload(probeType, "Probe ineligible", 100m, 10, 50m));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.ReadErrorsAsync()).Should().Contain(
            "The extended location amount applies only to the Regular and Premier tier types.");

        var persisted = await _fixture.QueryAsync(context =>
            context.Packages.CountAsync(p => p.PackageType == probeType));

        persisted.Should().Be(0);
    }

    [Fact]
    public async Task The_update_endpoint_should_clear_the_extended_amount_when_the_type_leaves_regular_or_premier()
    {
        var client = await _fixture.CreateAdministratorClientAsync();

        // A Regular tier that carries the amount.
        var create = await client.PostAsJsonAsync(
            "/api/packages",
            new PackagePayload("Regular", "Probe Regular extended", 950m, 80, 125m));
        create.StatusCode.Should().Be(HttpStatusCode.Created, await create.DescribeAsync());
        var packageId = (await create.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("packageId").GetInt32();
        var codeBefore = (await client.GetJsonAsync($"/api/packages/{packageId}"))
            .GetProperty("packageCode").GetString();

        // Change the type to Wedding and send no amount.
        var update = await client.PutAsJsonAsync(
            $"/api/packages/{packageId}",
            new PackagePayload("Wedding", "Probe Regular extended", 950m, 80, null));
        update.StatusCode.Should().Be(HttpStatusCode.NoContent, await update.DescribeAsync());

        var tier = await client.GetJsonAsync($"/api/packages/{packageId}");
        tier.GetProperty("extendedLocationAmount").ValueKind.Should().Be(JsonValueKind.Null);
        // The code is frozen: the type change never renumbers it.
        tier.GetProperty("packageCode").GetString().Should().Be(codeBefore);
    }

    // ─────────────────────────── helpers ───────────────────────────
    /// <summary>
    /// Creates the tier this test owns. Every test uses its own suffix, so the probes never
    /// collide.
    /// </summary>
    private static async Task<int> CreateProbeTierAsync(
        HttpClient client,
        string suffix,
        decimal? rate,
        int? minPax)
    {
        var response = await client.PostAsJsonAsync(
            "/api/packages",
            new PackagePayload($"{TierTypePrefix}{suffix}", $"Probe tier {suffix}", rate, minPax));

        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.DescribeAsync());

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("packageId").GetInt32();
    }
}

/// <summary>The payload of <c>POST /api/packages</c> and <c>PUT /api/packages/{id}</c> (spec §3.3).</summary>
internal sealed record PackagePayload(
    string PackageType,
    string Description,
    decimal? AmountPax,
    int? MinPax,
    decimal? ExtendedLocationAmount = null);
