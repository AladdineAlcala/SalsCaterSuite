using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SalsCaterSuite.Domain.Entities;
using SalsCaterSuite.Infrastructure.Persistence;
using SalsCaterSuite.IntegrationTests.Infrastructure;

namespace SalsCaterSuite.IntegrationTests;

/// <summary>
/// The Package Configuration Workbench endpoints against a real SQL Server (LocalDB) database
/// (contract §13, REQ-PKG-02).
/// <para>
/// Nothing is seeded (BR-13): every probe row — the tier, the municipality and the dish — is
/// created through the real API, and each test owns a uniquely suffixed probe so the probes never
/// collide.
/// </para>
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public class PackageWorkbenchEndpointTests
{
    private readonly IntegrationDatabaseFixture _fixture;

    public PackageWorkbenchEndpointTests(IntegrationDatabaseFixture fixture) => _fixture = fixture;

    // ─────────────────────────── the course ruleset ───────────────────────────

    [Fact]
    public async Task The_course_rules_endpoint_should_return_the_documented_envelope()
    {
        var client = await _fixture.CreateAdministratorClientAsync();
        var suffix = NewSuffix();

        var packageId = await CreateProbeTierAsync(client, suffix, 1450m, 150);
        var courseId = await GetCourseIdAsync(client, "Main Course");

        var response = await client.GetAsync($"/api/packages/{packageId}/course-rules");
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.DescribeAsync());

        var ruleset = await response.Content.ReadFromJsonAsync<JsonElement>();

        ruleset.GetProperty("packageId").GetInt32().Should().Be(packageId);
        ruleset.GetProperty("packageName").GetString().Should().Be($"Probe tier {suffix}");
        ruleset.GetProperty("packageType").GetString().Should().Be("PW");
        ruleset.GetProperty("amountPax").GetDecimal().Should().Be(1450m);
        ruleset.GetProperty("minPax").GetInt32().Should().Be(150);
        ruleset.GetProperty("isActive").GetBoolean().Should().BeTrue();

        // The benchmark is the base configuration's 35.0 — the host runs in the Testing
        // environment, so the ceiling assertions are deterministic.
        ruleset.GetProperty("targetFoodCostPercentage").GetDecimal().Should().Be(35.0m);
        ruleset.GetProperty("targetMarginPercentage").GetDecimal().Should().Be(65.0m);
        ruleset.GetProperty("isBenchmarkConfigured").GetBoolean().Should().BeTrue();

        // 1450 × 35 % = 507.50 and 1450 × 150 = 217 500.00.
        ruleset.GetProperty("costCeilingPerPax").GetDecimal().Should().Be(507.50m);
        ruleset.GetProperty("minimumContractValue").GetDecimal().Should().Be(217500.00m);

        // No rule is configured yet, so the planning figures are unknown — never zero.
        ruleset.GetProperty("standardRunFoodCostPerPax").ValueKind.Should().Be(JsonValueKind.Null);
        ruleset.GetProperty("standardRunFoodCostPercentage").ValueKind.Should().Be(JsonValueKind.Null);
        ruleset.GetProperty("baseGrossProfitPercentage").ValueKind.Should().Be(JsonValueKind.Null);

        ruleset.GetProperty("rules").GetArrayLength().Should().Be(0);
        ruleset.GetProperty("generatedAt").GetDateTimeOffset().Offset.Should().Be(TimeSpan.Zero);

        // The probe course is used so the rule's existence check is meaningful.
        courseId.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task The_course_rules_endpoint_should_publish_the_qualified_pool_and_the_standard_run()
    {
        var client = await _fixture.CreateAdministratorClientAsync();
        var suffix = NewSuffix();

        var packageId = await CreateProbeTierAsync(client, suffix, 1450m, 150);
        var courseId = await GetCourseIdAsync(client, "Main Course");
        var menuId = $"PW-{suffix}-A";

        await CreateProbeDishAsync(client, menuId, courseId, $"Probe dish {suffix}", 500m, 200m);

        var put = await client.PutAsJsonAsync(
            $"/api/packages/{packageId}/course-rules",
            new RulesetPayload(new[]
            {
                new RulePayload(courseId, 1, 120, "Exactly one main course", "Included", null)
            }));

        put.StatusCode.Should().Be(HttpStatusCode.OK, await put.DescribeAsync());

        var ruleset = await client.GetJsonAsync($"/api/packages/{packageId}/course-rules");

        var rule = ruleset.GetProperty("rules").EnumerateArray().Single();
        rule.GetProperty("courseId").GetInt32().Should().Be(courseId);
        rule.GetProperty("requiredQuota").GetInt32().Should().Be(1);
        rule.GetProperty("portionGrams").GetInt32().Should().Be(120);
        rule.GetProperty("ruleDescription").GetString().Should().Be("Exactly one main course");
        rule.GetProperty("inclusionTag").GetString().Should().Be("Included");
        rule.GetProperty("upgradeAddonId").ValueKind.Should().Be(JsonValueKind.Null);

        // The pool is the live dishes of the course.
        rule.GetProperty("poolSize").GetInt32().Should().BeGreaterThanOrEqualTo(1);
        rule.GetProperty("poolCostedCount").GetInt32().Should().BeGreaterThanOrEqualTo(1);
        rule.GetProperty("pool").GetArrayLength().Should().Be(rule.GetProperty("poolSize").GetInt32());

        // The standard run is the mean of the costed pool.
        ruleset.GetProperty("standardRunFoodCostPerPax").ValueKind.Should().NotBe(JsonValueKind.Null);
    }

    [Fact]
    public async Task The_put_course_rules_endpoint_should_reject_a_duplicate_course()
    {
        var client = await _fixture.CreateAdministratorClientAsync();
        var suffix = NewSuffix();

        var packageId = await CreateProbeTierAsync(client, suffix, 1000m, 50);
        var courseId = await GetCourseIdAsync(client, "Main Course");

        var response = await client.PutAsJsonAsync(
            $"/api/packages/{packageId}/course-rules",
            new RulesetPayload(new[]
            {
                new RulePayload(courseId, 1, null, "One main course", null, null),
                new RulePayload(courseId, 2, null, "The same course again", null, null)
            }));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.ReadErrorsAsync()).Should().Contain($"Course category {courseId} is configured more than once.");

        // Nothing was persisted.
        var persisted = await _fixture.QueryAsync(context =>
            context.PackageCourseRules.CountAsync(rule => rule.PackageId == packageId));

        persisted.Should().Be(0);
    }

    [Fact]
    public async Task The_put_course_rules_endpoint_should_reject_a_null_rules_member()
    {
        var client = await _fixture.CreateAdministratorClientAsync();
        var suffix = NewSuffix();

        var packageId = await CreateProbeTierAsync(client, suffix, 1000m, 50);

        var response = await client.PutAsJsonAsync(
            $"/api/packages/{packageId}/course-rules",
            new { rules = (object?)null });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.ReadErrorsAsync()).Should().Contain("Rules must be supplied.");
    }

    [Fact]
    public async Task The_put_course_rules_endpoint_should_replace_the_ruleset_and_soft_delete_the_removed_rule()
    {
        var client = await _fixture.CreateAdministratorClientAsync();
        var suffix = NewSuffix();

        var packageId = await CreateProbeTierAsync(client, suffix, 1000m, 50);
        var courseId = await GetCourseIdAsync(client, "Main Course");
        var otherCourseId = await GetCourseIdAsync(client, "Dessert");

        await client.PutAsJsonAsync(
            $"/api/packages/{packageId}/course-rules",
            new RulesetPayload(new[]
            {
                new RulePayload(courseId, 1, null, "One main course", null, null),
                new RulePayload(otherCourseId, 1, null, "One dessert", null, null)
            }));

        // Replace the ruleset with a single rule.
        var replace = await client.PutAsJsonAsync(
            $"/api/packages/{packageId}/course-rules",
            new RulesetPayload(new[]
            {
                new RulePayload(courseId, 1, null, "One main course", null, null)
            }));

        replace.StatusCode.Should().Be(HttpStatusCode.OK, await replace.DescribeAsync());

        var ruleset = await client.GetJsonAsync($"/api/packages/{packageId}/course-rules");
        ruleset.GetProperty("rules").GetArrayLength().Should().Be(1);

        // The removed rule is soft-deleted, never destroyed (DEC-085).
        var removed = await _fixture.QueryAsync(context =>
            context.PackageCourseRules
                .Where(rule => rule.PackageId == packageId && rule.CourseId == otherCourseId)
                .Select(rule => rule.IsDeleted)
                .ToListAsync());

        removed.Should().ContainSingle().Which.Should().BeTrue();
    }

    [Fact]
    public async Task An_empty_ruleset_should_clear_the_tier()
    {
        var client = await _fixture.CreateAdministratorClientAsync();
        var suffix = NewSuffix();

        var packageId = await CreateProbeTierAsync(client, suffix, 1000m, 50);
        var courseId = await GetCourseIdAsync(client, "Main Course");

        await client.PutAsJsonAsync(
            $"/api/packages/{packageId}/course-rules",
            new RulesetPayload(new[] { new RulePayload(courseId, 1, null, "One main course", null, null) }));

        var clear = await client.PutAsJsonAsync(
            $"/api/packages/{packageId}/course-rules",
            new RulesetPayload(Array.Empty<RulePayload>()));

        clear.StatusCode.Should().Be(HttpStatusCode.OK, await clear.DescribeAsync());

        var ruleset = await client.GetJsonAsync($"/api/packages/{packageId}/course-rules");
        ruleset.GetProperty("rules").GetArrayLength().Should().Be(0);

        var live = await _fixture.QueryAsync(context =>
            context.PackageCourseRules.CountAsync(rule => rule.PackageId == packageId && !rule.IsDeleted));

        live.Should().Be(0);
    }

    [Fact]
    public async Task A_re_added_course_should_not_create_a_duplicate_row()
    {
        var client = await _fixture.CreateAdministratorClientAsync();
        var suffix = NewSuffix();

        var packageId = await CreateProbeTierAsync(client, suffix, 1000m, 50);
        var courseId = await GetCourseIdAsync(client, "Main Course");

        var rules = new[] { new RulePayload(courseId, 1, null, "One main course", null, null) };

        await client.PutAsJsonAsync($"/api/packages/{packageId}/course-rules", new RulesetPayload(rules));
        await client.PutAsJsonAsync($"/api/packages/{packageId}/course-rules", new RulesetPayload(Array.Empty<RulePayload>()));
        await client.PutAsJsonAsync($"/api/packages/{packageId}/course-rules", new RulesetPayload(rules));

        // The natural key matched over ALL rows, so the soft-deleted row was resurrected rather
        // than a second row being inserted (DEC-088).
        var rows = await _fixture.QueryAsync(context =>
            context.PackageCourseRules.CountAsync(rule => rule.PackageId == packageId && rule.CourseId == courseId));

        rows.Should().Be(1);

        var live = await _fixture.QueryAsync(context =>
            context.PackageCourseRules.CountAsync(rule =>
                rule.PackageId == packageId && rule.CourseId == courseId && !rule.IsDeleted));

        live.Should().Be(1);
    }

    [Fact]
    public async Task The_database_should_refuse_a_second_live_row_for_the_same_natural_key()
    {
        var client = await _fixture.CreateAdministratorClientAsync();
        var suffix = NewSuffix();

        var packageId = await CreateProbeTierAsync(client, suffix, 1000m, 50);
        var courseId = await GetCourseIdAsync(client, "Main Course");

        await client.PutAsJsonAsync(
            $"/api/packages/{packageId}/course-rules",
            new RulesetPayload(new[] { new RulePayload(courseId, 1, null, "One main course", null, null) }));

        // The filtered unique index on (p_id, courseId) is the database-level guarantee (DEC-088).
        using var scope = _fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<SalsCaterSuiteDbContext>();

        context.PackageCourseRules.Add(new PackageCourseRule
        {
            PackageId = packageId,
            CourseId = courseId,
            RequiredQuota = 2,
            RuleDescription = "A second live rule"
        });

        var act = async () => await context.SaveChangesAsync();
        await act.Should().ThrowAsync<DbUpdateException>();
    }

    // ─────────────────────────── the selection simulator ───────────────────────────

    [Fact]
    public async Task The_course_rule_preview_should_return_the_expected_figures()
    {
        var client = await _fixture.CreateAdministratorClientAsync();
        var suffix = NewSuffix();

        var packageId = await CreateProbeTierAsync(client, suffix, 1000m, 50);
        var courseId = await GetCourseIdAsync(client, "Main Course");
        var menuId = $"PW-{suffix}-P";

        await CreateProbeDishAsync(client, menuId, courseId, $"Preview dish {suffix}", 500m, 100m);

        await client.PutAsJsonAsync(
            $"/api/packages/{packageId}/course-rules",
            new RulesetPayload(new[] { new RulePayload(courseId, 1, null, "Exactly one main course", null, null) }));

        var response = await client.PostAsJsonAsync(
            $"/api/packages/{packageId}/course-rules/preview",
            new PreviewPayload(new[] { new SelectionPayload(courseId, menuId) }, null, 120));

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.DescribeAsync());

        var preview = await response.Content.ReadFromJsonAsync<JsonElement>();

        preview.GetProperty("packageId").GetInt32().Should().Be(packageId);
        preview.GetProperty("amountPax").GetDecimal().Should().Be(1000m);
        preview.GetProperty("pax").GetInt32().Should().Be(120);
        preview.GetProperty("targetFoodCostPercentage").GetDecimal().Should().Be(35.0m);

        // 1000 × 35 % = 350.00.
        preview.GetProperty("costCeilingPerPax").GetDecimal().Should().Be(350.00m);
        preview.GetProperty("selectedFoodCostPerPax").GetDecimal().Should().Be(100.00m);
        preview.GetProperty("effectiveFoodCostPercentage").GetDecimal().Should().Be(10.00m);
        preview.GetProperty("isCostKnown").GetBoolean().Should().BeTrue();
        preview.GetProperty("isCeilingBreached").GetBoolean().Should().BeFalse();
        preview.GetProperty("ceilingShortfallPerPax").GetDecimal().Should().Be(0.00m);
        preview.GetProperty("ceilingOveragePercentage").GetDecimal().Should().Be(0.00m);

        // The headcount figures are the server's (AC-P2-28).
        preview.GetProperty("grossRevenue").GetDecimal().Should().Be(120000.00m);
        preview.GetProperty("foodCostTotal").GetDecimal().Should().Be(12000.00m);
        preview.GetProperty("grossProfit").GetDecimal().Should().Be(108000.00m);
        preview.GetProperty("grossProfitPercentage").GetDecimal().Should().Be(90.00m);

        preview.GetProperty("isQuotaCompliant").GetBoolean().Should().BeTrue();
        preview.GetProperty("isCompliant").GetBoolean().Should().BeTrue();
        preview.GetProperty("compliance").GetArrayLength().Should().Be(1);
        preview.GetProperty("selections").GetArrayLength().Should().Be(1);
    }

    [Fact]
    public async Task The_course_rule_preview_should_reject_an_unknown_dish()
    {
        var client = await _fixture.CreateAdministratorClientAsync();
        var suffix = NewSuffix();

        var packageId = await CreateProbeTierAsync(client, suffix, 1000m, 50);
        var courseId = await GetCourseIdAsync(client, "Main Course");

        var response = await client.PostAsJsonAsync(
            $"/api/packages/{packageId}/course-rules/preview",
            new PreviewPayload(new[] { new SelectionPayload(courseId, $"PW-{suffix}-MISSING") }, null, null));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.ReadErrorsAsync()).Should().Contain($"Dish PW-{suffix}-MISSING does not exist.");
    }

    [Fact]
    public async Task The_course_rule_preview_should_reject_a_null_selections_member()
    {
        var client = await _fixture.CreateAdministratorClientAsync();
        var suffix = NewSuffix();

        var packageId = await CreateProbeTierAsync(client, suffix, 1000m, 50);

        var response = await client.PostAsJsonAsync(
            $"/api/packages/{packageId}/course-rules/preview",
            new { selections = (object?)null, stressPercentage = (decimal?)null, pax = (int?)null });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.ReadErrorsAsync()).Should().Contain("Selections must be supplied.");
    }

    // ─────────────────────────── the municipality catalogue ───────────────────────────

    [Fact]
    public async Task The_area_catalogue_should_round_trip_through_the_api()
    {
        var client = await _fixture.CreateAdministratorClientAsync();
        var suffix = NewSuffix();

        var create = await client.PostAsJsonAsync(
            "/api/areas",
            new AreaPayload($"Probe municipality {suffix}", 150m, 42, "Reefer Truck & Mobile Gen", 3200m));

        create.StatusCode.Should().Be(HttpStatusCode.Created, await create.DescribeAsync());

        var created = await create.Content.ReadFromJsonAsync<JsonElement>();
        var areaId = created.GetProperty("areaId").GetInt32();
        areaId.Should().BeGreaterThan(0);

        var catalogue = await client.GetJsonAsync("/api/areas");
        var area = catalogue.EnumerateArray().Single(candidate => candidate.GetProperty("areaId").GetInt32() == areaId);
        area.GetProperty("areaDetails").GetString().Should().Be($"Probe municipality {suffix}");
        area.GetProperty("additionalRate").GetDecimal().Should().Be(150m);
        area.GetProperty("distanceKm").GetInt32().Should().Be(42);
        area.GetProperty("logisticsNote").GetString().Should().Be("Reefer Truck & Mobile Gen");
        area.GetProperty("logisticsCost").GetDecimal().Should().Be(3200m);

        var update = await client.PutAsJsonAsync(
            $"/api/areas/{areaId}",
            new AreaPayload($"Probe municipality {suffix} renamed", 175m, 44, "Van", 1500m));

        update.StatusCode.Should().Be(HttpStatusCode.NoContent, await update.DescribeAsync());

        var catalogueAfter = await client.GetJsonAsync("/api/areas");
        var updated = catalogueAfter.EnumerateArray().Single(candidate => candidate.GetProperty("areaId").GetInt32() == areaId);
        updated.GetProperty("areaDetails").GetString().Should().Be($"Probe municipality {suffix} renamed");
        updated.GetProperty("additionalRate").GetDecimal().Should().Be(175m);
        updated.GetProperty("distanceKm").GetInt32().Should().Be(44);
        updated.GetProperty("logisticsCost").GetDecimal().Should().Be(1500m);
    }

    [Fact]
    public async Task The_area_endpoints_should_reject_an_invalid_payload()
    {
        var client = await _fixture.CreateAdministratorClientAsync();

        var response = await client.PostAsJsonAsync(
            "/api/areas",
            new AreaPayload("   ", -1m, -1, new string('x', 151), -1m));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var errors = await response.ReadErrorsAsync();
        errors.Should().Contain("Area name is required.");
        errors.Should().Contain("Additional rate must be zero or greater.");
        errors.Should().Contain("Distance must be zero or greater.");
        errors.Should().Contain("Logistics note must not exceed 150 characters.");
        errors.Should().Contain("Logistics cost must be zero or greater.");
    }

    [Fact]
    public async Task The_update_area_endpoint_should_answer_not_found_for_an_unknown_municipality()
    {
        var client = await _fixture.CreateAdministratorClientAsync();

        var response = await client.PutAsJsonAsync(
            "/api/areas/99999999",
            new AreaPayload("Unknown municipality", null, null, null, null));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await response.ReadErrorsAsync()).Should().Contain("Area not found.");
    }

    // ─────────────────────────── the coverage matrix ───────────────────────────

    [Fact]
    public async Task The_area_attach_endpoint_should_return_created()
    {
        var client = await _fixture.CreateAdministratorClientAsync();
        var suffix = NewSuffix();

        var packageId = await CreateProbeTierAsync(client, suffix, 1000m, 50);
        var areaId = await CreateProbeAreaAsync(client, suffix, 150m, 42, "Reefer", 3200m);

        var response = await client.PostAsJsonAsync(
            $"/api/packages/{packageId}/areas",
            new AttachPayload(areaId, true));

        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.DescribeAsync());

        var created = await response.Content.ReadFromJsonAsync<JsonElement>();
        created.GetProperty("packageAreaCoverageId").GetInt32().Should().BeGreaterThan(0);

        var matrix = await client.GetJsonAsync($"/api/packages/{packageId}/areas");
        matrix.GetProperty("coverageCount").GetInt32().Should().Be(1);
        matrix.GetProperty("activeCoverageCount").GetInt32().Should().Be(1);
        matrix.GetProperty("lowestAdditionalRate").GetDecimal().Should().Be(150.00m);
        matrix.GetProperty("highestAdditionalRate").GetDecimal().Should().Be(150.00m);

        var row = matrix.GetProperty("areas").EnumerateArray().Single();
        row.GetProperty("areaId").GetInt32().Should().Be(areaId);
        row.GetProperty("additionalRate").GetDecimal().Should().Be(150.00m);
        row.GetProperty("distanceKm").GetInt32().Should().Be(42);
        row.GetProperty("logisticsNote").GetString().Should().Be("Reefer");
        row.GetProperty("logisticsCost").GetDecimal().Should().Be(3200.00m);
        row.GetProperty("isActive").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task The_area_attach_endpoint_should_refuse_an_unknown_municipality()
    {
        var client = await _fixture.CreateAdministratorClientAsync();
        var suffix = NewSuffix();

        var packageId = await CreateProbeTierAsync(client, suffix, 1000m, 50);

        var response = await client.PostAsJsonAsync(
            $"/api/packages/{packageId}/areas",
            new AttachPayload(99999999, true));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.ReadErrorsAsync()).Should().Contain("Area 99999999 does not exist.");
    }

    [Fact]
    public async Task The_put_areas_endpoint_should_reject_a_null_coverages_member()
    {
        var client = await _fixture.CreateAdministratorClientAsync();
        var suffix = NewSuffix();

        var packageId = await CreateProbeTierAsync(client, suffix, 1000m, 50);

        var response = await client.PutAsJsonAsync(
            $"/api/packages/{packageId}/areas",
            new { coverages = (object?)null });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.ReadErrorsAsync()).Should().Contain("Coverages must be supplied.");
    }

    [Fact]
    public async Task An_empty_coverage_set_should_clear_the_tier()
    {
        var client = await _fixture.CreateAdministratorClientAsync();
        var suffix = NewSuffix();

        var packageId = await CreateProbeTierAsync(client, suffix, 1000m, 50);
        var areaId = await CreateProbeAreaAsync(client, suffix, 150m, 42, "Reefer", 3200m);

        await client.PostAsJsonAsync($"/api/packages/{packageId}/areas", new AttachPayload(areaId, true));

        var clear = await client.PutAsJsonAsync(
            $"/api/packages/{packageId}/areas",
            new { coverages = Array.Empty<AttachPayload>() });

        clear.StatusCode.Should().Be(HttpStatusCode.OK, await clear.DescribeAsync());

        var matrix = await client.GetJsonAsync($"/api/packages/{packageId}/areas");
        matrix.GetProperty("coverageCount").GetInt32().Should().Be(0);
        matrix.GetProperty("activeCoverageCount").GetInt32().Should().Be(0);

        var live = await _fixture.QueryAsync(context =>
            context.PackageAreaCoverages.CountAsync(coverage =>
                coverage.PackageId == packageId && !coverage.IsDeleted));

        live.Should().Be(0);
    }

    // ─────────────────────────── the location simulator ───────────────────────────

    [Fact]
    public async Task The_location_preview_should_return_the_expected_breakdown()
    {
        var client = await _fixture.CreateAdministratorClientAsync();
        var suffix = NewSuffix();

        var packageId = await CreateProbeTierAsync(client, suffix, 1000m, 50);
        var courseId = await GetCourseIdAsync(client, "Main Course");
        var menuId = $"PW-{suffix}-L";

        await CreateProbeDishAsync(client, menuId, courseId, $"Location dish {suffix}", 500m, 100m);
        await client.PutAsJsonAsync(
            $"/api/packages/{packageId}/course-rules",
            new RulesetPayload(new[] { new RulePayload(courseId, 1, null, "Exactly one main course", null, null) }));

        var areaId = await CreateProbeAreaAsync(client, suffix, 150m, 42, "Reefer", 3200m);

        var response = await client.PostAsJsonAsync(
            $"/api/packages/{packageId}/areas/preview",
            new LocationPayload(50, areaId, Array.Empty<SelectionPayload>()));

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.DescribeAsync());

        var preview = await response.Content.ReadFromJsonAsync<JsonElement>();

        preview.GetProperty("packageId").GetInt32().Should().Be(packageId);
        preview.GetProperty("pax").GetInt32().Should().Be(50);
        preview.GetProperty("areaId").GetInt32().Should().Be(areaId);
        preview.GetProperty("additionalRate").GetDecimal().Should().Be(150.00m);

        // 1000 × 50 = 50 000.00 and 150 × 50 = 7 500.00.
        preview.GetProperty("baseTotal").GetDecimal().Should().Be(50000.00m);
        preview.GetProperty("extendedTotal").GetDecimal().Should().Be(7500.00m);
        preview.GetProperty("effectiveRatePerPax").GetDecimal().Should().Be(1150.00m);
        preview.GetProperty("totalContractValue").GetDecimal().Should().Be(57500.00m);

        // No selection was supplied, so the food cost is the standard run.
        preview.GetProperty("foodCostPerPax").ValueKind.Should().NotBe(JsonValueKind.Null);
        preview.GetProperty("foodCostTotal").ValueKind.Should().NotBe(JsonValueKind.Null);
        preview.GetProperty("logisticsCost").GetDecimal().Should().Be(3200.00m);
        preview.GetProperty("grossProfit").ValueKind.Should().NotBe(JsonValueKind.Null);
        preview.GetProperty("grossProfitPercentage").ValueKind.Should().NotBe(JsonValueKind.Null);
    }

    [Fact]
    public async Task The_location_preview_should_reject_a_headcount_below_one()
    {
        var client = await _fixture.CreateAdministratorClientAsync();
        var suffix = NewSuffix();

        var packageId = await CreateProbeTierAsync(client, suffix, 1000m, 50);

        var response = await client.PostAsJsonAsync(
            $"/api/packages/{packageId}/areas/preview",
            new LocationPayload(0, null, Array.Empty<SelectionPayload>()));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.ReadErrorsAsync()).Should().Contain("Pax must be at least 1.");
    }

    // ─────────────────────────── the specification sheet ───────────────────────────

    [Fact]
    public async Task The_spec_sheet_should_return_a_pdf()
    {
        var client = await _fixture.CreateAdministratorClientAsync();
        var suffix = NewSuffix();

        var packageId = await CreateProbeTierAsync(client, suffix, 1450m, 150);
        var courseId = await GetCourseIdAsync(client, "Main Course");

        await client.PutAsJsonAsync(
            $"/api/packages/{packageId}/course-rules",
            new RulesetPayload(new[] { new RulePayload(courseId, 1, null, "One main course", null, null) }));

        var response = await client.GetAsync($"/api/packages/{packageId}/spec-sheet");

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.DescribeAsync());
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/pdf");

        var bytes = await response.Content.ReadAsByteArrayAsync();
        bytes.Length.Should().BeGreaterThan(0);

        // The PDF magic number.
        bytes.Take(4).Should().Equal((byte)'%', (byte)'P', (byte)'D', (byte)'F');
    }

    // ─────────────────────────── the tier population (DEC-074) ───────────────────────────

    [Fact]
    public async Task Every_tier_scoped_endpoint_should_answer_not_found_for_a_soft_deleted_tier()
    {
        var client = await _fixture.CreateAdministratorClientAsync();
        var suffix = NewSuffix();

        var packageId = await CreateProbeTierAsync(client, suffix, 1000m, 50);
        var courseId = await GetCourseIdAsync(client, "Main Course");
        var areaId = await CreateProbeAreaAsync(client, suffix, 150m, 42, "Reefer", 3200m);

        await client.PutAsJsonAsync(
            $"/api/packages/{packageId}/course-rules",
            new RulesetPayload(new[] { new RulePayload(courseId, 1, null, "One main course", null, null) }));
        await client.PostAsJsonAsync($"/api/packages/{packageId}/areas", new AttachPayload(areaId, true));

        await SoftDeleteTierAsync(packageId);

        // The eight tier-scoped endpoints of the eleven.
        var responses = new[]
        {
            await client.GetAsync($"/api/packages/{packageId}/course-rules"),
            await client.PutAsJsonAsync(
                $"/api/packages/{packageId}/course-rules",
                new RulesetPayload(new[] { new RulePayload(courseId, 1, null, "One main course", null, null) })),
            await client.PostAsJsonAsync(
                $"/api/packages/{packageId}/course-rules/preview",
                new PreviewPayload(Array.Empty<SelectionPayload>(), null, null)),
            await client.GetAsync($"/api/packages/{packageId}/areas"),
            await client.PutAsJsonAsync(
                $"/api/packages/{packageId}/areas",
                new { coverages = new[] { new AttachPayload(areaId, true) } }),
            await client.PostAsJsonAsync($"/api/packages/{packageId}/areas", new AttachPayload(areaId, true)),
            await client.PostAsJsonAsync(
                $"/api/packages/{packageId}/areas/preview",
                new LocationPayload(50, areaId, Array.Empty<SelectionPayload>())),
            await client.GetAsync($"/api/packages/{packageId}/spec-sheet")
        };

        foreach (var response in responses)
        {
            response.StatusCode.Should().Be(
                HttpStatusCode.NotFound,
                $"{response.RequestMessage?.Method} {response.RequestMessage?.RequestUri} must answer 404 for a soft-deleted tier");

            (await response.ReadErrorsAsync()).Should().Contain("Package not found.");
        }

        // No write touched the tier (AC-P2-21).
        var liveRules = await _fixture.QueryAsync(context =>
            context.PackageCourseRules.CountAsync(rule => rule.PackageId == packageId && !rule.IsDeleted));
        liveRules.Should().Be(1);

        var liveCoverages = await _fixture.QueryAsync(context =>
            context.PackageAreaCoverages.CountAsync(coverage => coverage.PackageId == packageId && !coverage.IsDeleted));
        liveCoverages.Should().Be(1);
    }

    // ─────────────────────────── helpers ───────────────────────────

    /// <summary>A unique suffix so every probe owns its own rows.</summary>
    private static string NewSuffix() => Guid.NewGuid().ToString("N")[..8];

    private static async Task<int> CreateProbeTierAsync(
        HttpClient client,
        string suffix,
        decimal? rate,
        int? minPax)
    {
        var response = await client.PostAsJsonAsync(
            "/api/packages",
            new PackagePayload("PW", $"Probe tier {suffix}", rate, minPax));

        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.DescribeAsync());

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("packageId").GetInt32();
    }

    private static async Task<int> CreateProbeAreaAsync(
        HttpClient client,
        string suffix,
        decimal? rate,
        int? distance,
        string? note,
        decimal? logisticsCost)
    {
        var response = await client.PostAsJsonAsync(
            "/api/areas",
            new AreaPayload($"Probe municipality {suffix}", rate, distance, note, logisticsCost));

        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.DescribeAsync());

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("areaId").GetInt32();
    }

    private static async Task<string> CreateProbeDishAsync(
        HttpClient client,
        string menuId,
        int courseId,
        string menuName,
        decimal price,
        decimal foodCost)
    {
        var response = await client.PostAsJsonAsync(
            "/api/menus",
            new DishPayload(menuId, courseId, menuName, price, foodCost));

        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.DescribeAsync());
        return menuId;
    }

    /// <summary>The id of a seeded course category — a system reference row, never business data.</summary>
    private static async Task<int> GetCourseIdAsync(HttpClient client, string courseName)
    {
        var courses = await client.GetJsonAsync("/api/menus/courses");

        var course = courses.EnumerateArray()
            .Single(candidate => candidate.GetProperty("courseName").GetString() == courseName);

        return course.GetProperty("courseId").GetInt32();
    }

    /// <summary>Flags the probe tier as soft-deleted — the state the catalogue population excludes.</summary>
    private async Task SoftDeleteTierAsync(int packageId)
    {
        using var scope = _fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<SalsCaterSuiteDbContext>();

        var package = await context.Packages.FirstAsync(candidate => candidate.PackageId == packageId);
        package.IsDeleted = true;
        await context.SaveChangesAsync();
    }
}

/// <summary>The body of <c>PUT /api/packages/{id}/course-rules</c>.</summary>
internal sealed record RulesetPayload(RulePayload[] Rules);

/// <summary>One submitted course rule.</summary>
internal sealed record RulePayload(
    int CourseId,
    int RequiredQuota,
    int? PortionGrams,
    string RuleDescription,
    string? InclusionTag,
    int? UpgradeAddonId);

/// <summary>The body of <c>POST /api/packages/{id}/course-rules/preview</c>.</summary>
internal sealed record PreviewPayload(
    SelectionPayload[] Selections,
    decimal? StressPercentage,
    int? Pax);

/// <summary>One selected dish of a preview.</summary>
internal sealed record SelectionPayload(int CourseId, string MenuId);

/// <summary>The body of <c>POST /api/packages/{id}/areas/preview</c>.</summary>
internal sealed record LocationPayload(
    int Pax,
    int? AreaId,
    SelectionPayload[] Selections);

/// <summary>The body of <c>POST /api/packages/{id}/areas</c>.</summary>
internal sealed record AttachPayload(int AreaId, bool IsActive);

/// <summary>The body of <c>POST /api/areas</c> and <c>PUT /api/areas/{id}</c>.</summary>
internal sealed record AreaPayload(
    string AreaDetails,
    decimal? AdditionalRate,
    int? DistanceKm,
    string? LogisticsNote,
    decimal? LogisticsCost);

/// <summary>The body of <c>POST /api/menus</c>.</summary>
internal sealed record DishPayload(
    string MenuId,
    int CourseId,
    string MenuName,
    decimal Price,
    decimal FoodCost);
