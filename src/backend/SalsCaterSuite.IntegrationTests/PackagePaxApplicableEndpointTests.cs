using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SalsCaterSuite.IntegrationTests.Infrastructure;

namespace SalsCaterSuite.IntegrationTests;

/// <summary>
/// The applicable-pax classification lookup and the required package classification
/// (REQ-PKG-04, DEC-173, contract §12.8) against a real SQL Server (LocalDB) database.
/// <para>
/// The lookup ships with the five fixed rows the schema migration seeds, so this test class
/// reads the reference data from the database rather than fabricating it.
/// </para>
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public class PackagePaxApplicableEndpointTests
{
    /// <summary>The distinctive prefix of the tier types this test class owns.</summary>
    private const string TierTypePrefix = "PXP";

    private readonly IntegrationDatabaseFixture _fixture;

    public PackagePaxApplicableEndpointTests(IntegrationDatabaseFixture fixture) => _fixture = fixture;

    // ─────────────────────────── the lookup endpoint ───────────────────────────

    [Fact]
    public async Task The_lookup_endpoint_should_require_authentication()
    {
        var client = _fixture.Factory.CreateClient();

        var response = await client.GetAsync("/api/packages/pax-applicables");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task The_lookup_endpoint_should_return_the_five_fixed_options_in_identifier_order()
    {
        var client = await _fixture.CreateAdministratorClientAsync();

        var response = await client.GetAsync("/api/packages/pax-applicables");
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.DescribeAsync());

        var options = await response.Content.ReadFromJsonAsync<JsonElement>();

        options.EnumerateArray().Select(option => option.GetProperty("numPaxId").GetInt32())
            .Should().ContainInOrder(1, 2, 3, 4, 5);
    }

    [Fact]
    public async Task The_lookup_endpoint_should_publish_the_labels_verbatim()
    {
        // The skipped ranges and the "80 to 100 Above" wording are intentional (DEC-173). They
        // are stored and published exactly as the owner supplied them.
        var client = await _fixture.CreateAdministratorClientAsync();

        var options = await client.GetJsonAsync("/api/packages/pax-applicables");

        options.EnumerateArray()
            .Select(option => option.GetProperty("packageApplicablePax").GetString())
            .Should().ContainInOrder(
                "15 to 19 Pax",
                "20 to 29 Pax",
                "30 to 49 Pax",
                "80 to 100 Above",
                "Not Applicable to PackMeals");
    }

    [Fact]
    public async Task The_lookup_endpoint_should_publish_exactly_the_two_documented_members()
    {
        // No parsed interval is published: the classification is a label, not a range.
        var client = await _fixture.CreateAdministratorClientAsync();

        var options = await client.GetJsonAsync("/api/packages/pax-applicables");

        foreach (var option in options.EnumerateArray())
        {
            option.EnumerateObject().Select(property => property.Name)
                .Should().BeEquivalentTo(new[] { "numPaxId", "packageApplicablePax" });
        }
    }

    [Fact]
    public async Task The_seeded_lookup_rows_should_be_live_and_carry_truthful_audit_columns()
    {
        // The rows are seeded by the migration with IsDeleted = false, a real creation timestamp,
        // a NULL creator (a schema migration has no human author) and a NULL update timestamp.
        var rows = await _fixture.QueryAsync(context => context.PackagePaxApplicables
            .OrderBy(option => option.NumPaxId)
            .ToListAsync());

        rows.Should().HaveCount(5);

        foreach (var row in rows)
        {
            row.IsDeleted.Should().BeFalse();
            row.CreatedDate.Should().NotBeNull();
            row.CreatedByUser.Should().BeNull("a schema migration has no human author");
            row.UpdatedDate.Should().BeNull("the reference rows have never been updated");
        }
    }

    [Fact]
    public async Task The_seeded_lookup_rows_should_have_no_database_generated_identity()
    {
        // The five identifiers are fixed reference data, so the primary key is not an identity.
        var isIdentity = await _fixture.QueryAsync(async context =>
        {
            var connection = context.Database.GetDbConnection();
            var mustClose = connection.State != System.Data.ConnectionState.Open;

            if (mustClose)
            {
                await connection.OpenAsync();
            }

            try
            {
                await using var command = connection.CreateCommand();
                command.CommandText =
                    "SELECT COUNT(*) FROM sys.identity_columns WHERE object_id = OBJECT_ID('PackagePaxApplicable')";

                var scalar = await command.ExecuteScalarAsync();
                return Convert.ToInt32(scalar);
            }
            finally
            {
                if (mustClose)
                {
                    await connection.CloseAsync();
                }
            }
        });

        isIdentity.Should().Be(0, "the lookup's primary key is fixed reference data, not generated");
    }

    // ─────────────────────────── the required classification ───────────────────────────

    [Fact]
    public async Task The_create_endpoint_should_require_the_classification_and_persist_nothing_without_it()
    {
        var client = await _fixture.CreateAdministratorClientAsync();
        var probeType = $"{TierTypePrefix}NOID";

        var response = await client.PostAsJsonAsync(
            "/api/packages",
            new
            {
                packageType = probeType,
                description = "Probe without a classification",
                amountPax = 100m,
                minPax = 10
            });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.ReadErrorsAsync()).Should().Contain("Applicable pax classification is required.");

        var persisted = await _fixture.QueryAsync(context =>
            context.Packages.CountAsync(p => p.PackageType == probeType));

        persisted.Should().Be(0);
    }

    [Fact]
    public async Task The_create_endpoint_should_reject_an_unknown_classification_and_persist_nothing()
    {
        var client = await _fixture.CreateAdministratorClientAsync();
        var probeType = $"{TierTypePrefix}UNKNOWN";

        var response = await client.PostAsJsonAsync(
            "/api/packages",
            new
            {
                packageType = probeType,
                description = "Probe with an unknown classification",
                amountPax = 100m,
                minPax = 10,
                numPaxId = 999
            });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.ReadErrorsAsync()).Should().Contain("Applicable pax classification was not found.");

        var persisted = await _fixture.QueryAsync(context =>
            context.Packages.CountAsync(p => p.PackageType == probeType));

        persisted.Should().Be(0);
    }

    [Fact]
    public async Task The_create_and_update_endpoints_should_store_and_publish_the_classification()
    {
        var client = await _fixture.CreateAdministratorClientAsync();
        var probeType = $"{TierTypePrefix}OK";

        // Create with the "80 to 100 Above" classification (id 4).
        var created = await client.PostAsJsonAsync(
            "/api/packages",
            new
            {
                packageType = probeType,
                description = "Probe with a classification",
                amountPax = 1200m,
                minPax = 100,
                numPaxId = 4
            });

        created.StatusCode.Should().Be(HttpStatusCode.Created, await created.DescribeAsync());

        var packageId = (await created.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("packageId").GetInt32();

        // The catalogue list and the tier detail both publish it.
        var tier = await client.GetJsonAsync($"/api/packages/{packageId}");
        tier.GetProperty("numPaxId").GetInt32().Should().Be(4);

        var list = await client.GetJsonAsync("/api/packages");
        list.EnumerateArray()
            .Single(item => item.GetProperty("packageId").GetInt32() == packageId)
            .GetProperty("numPaxId").GetInt32().Should().Be(4);

        // Update it to the "20 to 29 Pax" classification (id 2).
        var updated = await client.PutAsJsonAsync(
            $"/api/packages/{packageId}",
            new
            {
                packageType = probeType,
                description = "Probe with a classification",
                amountPax = 1200m,
                minPax = 100,
                numPaxId = 2
            });

        updated.StatusCode.Should().Be(HttpStatusCode.NoContent, await updated.DescribeAsync());

        var afterUpdate = await client.GetJsonAsync($"/api/packages/{packageId}");
        afterUpdate.GetProperty("numPaxId").GetInt32().Should().Be(2);
    }

    [Fact]
    public async Task The_update_endpoint_should_reject_an_unknown_classification_and_change_nothing()
    {
        var client = await _fixture.CreateAdministratorClientAsync();

        var created = await client.PostAsJsonAsync(
            "/api/packages",
            new
            {
                packageType = $"{TierTypePrefix}UPD",
                description = "Probe for the update guard",
                amountPax = 1200m,
                minPax = 100,
                numPaxId = 4
            });

        created.StatusCode.Should().Be(HttpStatusCode.Created, await created.DescribeAsync());

        var packageId = (await created.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("packageId").GetInt32();

        var response = await client.PutAsJsonAsync(
            $"/api/packages/{packageId}",
            new
            {
                packageType = $"{TierTypePrefix}UPD",
                description = "Probe for the update guard",
                amountPax = 1200m,
                minPax = 100,
                numPaxId = 999
            });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.ReadErrorsAsync()).Should().Contain("Applicable pax classification was not found.");

        // The stored tier keeps its previous classification and description.
        var tier = await client.GetJsonAsync($"/api/packages/{packageId}");
        tier.GetProperty("numPaxId").GetInt32().Should().Be(4);
    }

    [Fact]
    public async Task The_classification_should_not_become_a_booking_or_pricing_rule()
    {
        // AC-P19 / DEC-173: the classification is descriptive metadata. It does not constrain the
        // tier type, the minimum headcount, the booking eligibility or the price. The same tier
        // with a different classification behaves identically.
        var client = await _fixture.CreateAdministratorClientAsync();

        var created = await client.PostAsJsonAsync(
            "/api/packages",
            new
            {
                packageType = "Pm",
                description = "Probe pack-meals tier",
                amountPax = 800m,
                minPax = 20,
                numPaxId = 5
            });

        created.StatusCode.Should().Be(HttpStatusCode.Created, await created.DescribeAsync());

        var tier = (await created.Content.ReadFromJsonAsync<JsonElement>());

        // The "Not Applicable to PackMeals" option is not coupled to PackageType.Pm and does not
        // change the tier's own figures.
        var detail = await client.GetJsonAsync($"/api/packages/{tier.GetProperty("packageId").GetInt32()}");
        detail.GetProperty("packageType").GetString().Should().Be("Pm");
        detail.GetProperty("minPax").GetInt32().Should().Be(20);
        detail.GetProperty("amountPax").GetDecimal().Should().Be(800m);
        detail.GetProperty("numPaxId").GetInt32().Should().Be(5);
    }
}
