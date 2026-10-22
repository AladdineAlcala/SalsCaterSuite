using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SalsCaterSuite.Domain.Entities;
using SalsCaterSuite.IntegrationTests.Infrastructure;

namespace SalsCaterSuite.IntegrationTests;

/// <summary>
/// The Menu &amp; Food Costing Master endpoints against a real SQL Server (LocalDB) database
/// (spec §6.1 / §6.2 / §6.3, REQ-MNU-01/REQ-MNU-02).
/// <para>
/// The <c>Menu</c> table starts empty (BR-13 forbids seeding business data), so every test
/// creates the dishes it needs through the real API and scopes its assertions to its own
/// uniquely-suffixed probe dishes.
/// </para>
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public class MenuCostingEndpointTests
{
    /// <summary>The distinctive prefix of the dish codes this test class owns.</summary>
    private const string DishCodePrefix = "MCP";

    /// <summary>
    /// The configured target food-cost percentage. The integration host runs the Testing
    /// environment, so the base <c>appsettings.json</c> applies: the benchmark is 35.0 % and it
    /// <b>is</b> configured (DEC-040).
    /// </summary>
    private const decimal TargetFoodCostPercentage = 35.0m;

    private readonly IntegrationDatabaseFixture _fixture;

    public MenuCostingEndpointTests(IntegrationDatabaseFixture fixture) => _fixture = fixture;

    // ─────────────────────────── the master endpoint ───────────────────────────

    [Fact]
    public async Task The_master_endpoint_should_return_the_paged_payload()
    {
        var client = await _fixture.CreateAdministratorClientAsync();
        var courseId = await GetCourseIdAsync(client, "Main Course");

        // 420.00 / 138.60 → 33.00 % food cost, 67.00 % margin.
        await CreateProbeDishAsync(client, "AAA", courseId, 420m, 138.60m);

        var response = await client.GetAsync($"/api/menus/master?search={DishCodePrefix}AAA");
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.DescribeAsync());

        var master = await response.Content.ReadFromJsonAsync<JsonElement>();

        master.GetProperty("page").GetInt32().Should().Be(1);
        master.GetProperty("pageSize").GetInt32().Should().Be(12);
        master.GetProperty("totalCount").GetInt32().Should().Be(1);
        master.GetProperty("totalPages").GetInt32().Should().Be(1);
        master.GetProperty("generatedAt").GetDateTimeOffset().Offset.Should().Be(TimeSpan.Zero);

        var item = master.GetProperty("items").EnumerateArray().Single();
        item.GetProperty("menuId").GetString().Should().Be($"{DishCodePrefix}AAA");
        item.GetProperty("courseId").GetInt32().Should().Be(courseId);
        item.GetProperty("courseName").GetString().Should().Be("Main Course");
        item.GetProperty("sellingPrice").GetDecimal().Should().Be(420m);
        item.GetProperty("foodCost").GetDecimal().Should().Be(138.60m);
        item.GetProperty("grossProfit").GetDecimal().Should().Be(281.40m);
        item.GetProperty("foodCostPercentage").GetDecimal().Should().Be(33.00m);
        item.GetProperty("grossMarginPercentage").GetDecimal().Should().Be(67.00m);
        item.GetProperty("targetFoodCostPercentage").GetDecimal().Should().Be(TargetFoodCostPercentage);
        item.GetProperty("isWithinTarget").GetBoolean().Should().BeTrue();
        item.GetProperty("marginVariancePercentage").GetDecimal().Should().Be(-2.00m);

        var summary = master.GetProperty("summary");
        summary.GetProperty("totalDishes").GetInt32().Should().Be(1);
        summary.GetProperty("costedDishes").GetInt32().Should().Be(1);
        summary.GetProperty("uncostedDishes").GetInt32().Should().Be(0);
        summary.GetProperty("costingCoveragePercentage").GetDecimal().Should().Be(100m);
        summary.GetProperty("averageFoodCostPercentage").GetDecimal().Should().Be(33.00m);
        summary.GetProperty("averageFoodCostHeadroomPercentage").GetDecimal().Should().Be(2.00m);
        summary.GetProperty("targetFoodCostPercentage").GetDecimal().Should().Be(TargetFoodCostPercentage);
        summary.GetProperty("targetMarginPercentage").GetDecimal().Should().Be(65.00m);
        summary.GetProperty("isBenchmarkConfigured").GetBoolean().Should().BeTrue();
        summary.GetProperty("averageSellingPrice").GetDecimal().Should().Be(420m);
        summary.GetProperty("marginAlertCount").GetInt32().Should().Be(0);

        var best = summary.GetProperty("bestMarginCourse");
        best.GetProperty("courseId").GetInt32().Should().Be(courseId);
        best.GetProperty("courseName").GetString().Should().Be("Main Course");
        best.GetProperty("grossMarginPercentage").GetDecimal().Should().Be(67.00m);
        best.GetProperty("averageSellingPrice").GetDecimal().Should().Be(420m);
        best.GetProperty("dishCount").GetInt32().Should().Be(1);

        // The course tab strip — the scope's total and one entry per course category.
        var courses = master.GetProperty("courses");
        courses.GetProperty("total").GetInt32().Should().Be(1);
        courses.GetProperty("items").EnumerateArray().Should().NotBeEmpty();
    }

    [Fact]
    public async Task The_master_endpoint_should_clamp_the_paging_and_ignore_an_unknown_filter()
    {
        var client = await _fixture.CreateAdministratorClientAsync();

        // page = 0 and pageSize = 1000 are clamped, never rejected.
        var clamped = await client.GetAsync("/api/menus/master?page=0&pageSize=1000");
        clamped.StatusCode.Should().Be(HttpStatusCode.OK, await clamped.DescribeAsync());

        var master = await clamped.Content.ReadFromJsonAsync<JsonElement>();
        master.GetProperty("page").GetInt32().Should().Be(1);
        master.GetProperty("pageSize").GetInt32().Should().Be(100);

        // An unknown status and an unknown sort are ignored, not a 400.
        var ignored = await client.GetAsync("/api/menus/master?status=banana&sort=banana");
        ignored.StatusCode.Should().Be(HttpStatusCode.OK, await ignored.DescribeAsync());
    }

    [Fact]
    public async Task The_master_endpoint_should_filter_by_status()
    {
        var client = await _fixture.CreateAdministratorClientAsync();
        var courseId = await GetCourseIdAsync(client, "Main Course");

        // 100.00 / 40.00 → 40 % — above the 35 % target.
        await CreateProbeDishAsync(client, "BBB", courseId, 100m, 40m);
        // A dish without a food cost — uncosted.
        await CreateProbeDishAsync(client, "CCC", courseId, 100m, 0m);

        var alerts = await client.GetJsonAsync($"/api/menus/master?search={DishCodePrefix}BBB&status=MarginAlert");
        alerts.GetProperty("items").EnumerateArray().Should().ContainSingle()
            .Which.GetProperty("menuId").GetString().Should().Be($"{DishCodePrefix}BBB");
        alerts.GetProperty("summary").GetProperty("marginAlertCount").GetInt32().Should().Be(1);

        var uncosted = await client.GetJsonAsync($"/api/menus/master?search={DishCodePrefix}CCC&status=Uncosted");
        uncosted.GetProperty("items").EnumerateArray().Should().ContainSingle()
            .Which.GetProperty("menuId").GetString().Should().Be($"{DishCodePrefix}CCC");
        uncosted.GetProperty("summary").GetProperty("costedDishes").GetInt32().Should().Be(0);
        uncosted.GetProperty("summary").GetProperty("uncostedDishes").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task The_master_endpoint_should_sort_the_dishes()
    {
        var client = await _fixture.CreateAdministratorClientAsync();
        var courseId = await GetCourseIdAsync(client, "Main Course");

        await CreateProbeDishAsync(client, "DDD", courseId, 300m, 90m);  // 30 %
        await CreateProbeDishAsync(client, "EEE", courseId, 100m, 50m);  // 50 %

        var ascending = await client.GetJsonAsync($"/api/menus/master?search={DishCodePrefix}&sort=FoodCostAsc&pageSize=100");
        var descending = await client.GetJsonAsync($"/api/menus/master?search={DishCodePrefix}&sort=FoodCostDesc&pageSize=100");

        // The catalogue holds more than the two probe dishes, so only the probe dishes are
        // asserted — the sort is proven by their relative order.
        var ascendingIds = ascending.GetProperty("items").EnumerateArray()
            .Select(i => i.GetProperty("menuId").GetString())
            .Where(id => id is not null && id.StartsWith(DishCodePrefix, StringComparison.Ordinal))
            .ToList();
        var descendingIds = descending.GetProperty("items").EnumerateArray()
            .Select(i => i.GetProperty("menuId").GetString())
            .Where(id => id is not null && id.StartsWith(DishCodePrefix, StringComparison.Ordinal))
            .ToList();

        ascendingIds.Should().ContainInOrder($"{DishCodePrefix}DDD", $"{DishCodePrefix}EEE");
        descendingIds.Should().ContainInOrder($"{DishCodePrefix}EEE", $"{DishCodePrefix}DDD");
    }

    [Fact]
    public async Task The_master_endpoint_should_exclude_a_soft_deleted_dish()
    {
        var client = await _fixture.CreateAdministratorClientAsync();
        var courseId = await GetCourseIdAsync(client, "Main Course");

        await CreateProbeDishAsync(client, "FFF", courseId, 200m, 50m);
        await SoftDeleteDishAsync($"{DishCodePrefix}FFF");

        var master = await client.GetJsonAsync($"/api/menus/master?search={DishCodePrefix}FFF");

        master.GetProperty("items").EnumerateArray().Should().BeEmpty();
        master.GetProperty("totalCount").GetInt32().Should().Be(0);
        master.GetProperty("summary").GetProperty("totalDishes").GetInt32().Should().Be(0);
    }

    [Fact]
    public async Task The_master_figures_should_reconcile_with_the_dashboard()
    {
        var client = await _fixture.CreateAdministratorClientAsync();
        var courseId = await GetCourseIdAsync(client, "Main Course");

        await CreateProbeDishAsync(client, "GGG", courseId, 350m, 105m);

        var master = await client.GetJsonAsync("/api/menus/master?pageSize=100");
        var dashboard = await client.GetJsonAsync("/api/dashboard");

        var summary = master.GetProperty("summary");
        var margin = dashboard.GetProperty("margin");
        var dishCosting = dashboard.GetProperty("dishCosting");

        // KPI-15: the dashboard's catalogue margin is the complement of the master's average
        // food cost — the two views of one aggregate can never disagree (AC-M4, AC-M13).
        var catalogueMargin = margin.GetProperty("catalogueMarginPercentage").GetDecimal();
        var averageFoodCost = summary.GetProperty("averageFoodCostPercentage").GetDecimal();

        (100m - averageFoodCost).Should().Be(catalogueMargin);

        // KPI-14: the costing coverage.
        summary.GetProperty("costingCoveragePercentage").GetDecimal()
            .Should().Be(dishCosting.GetProperty("coveragePercentage").GetDecimal());

        // The populations agree as well.
        summary.GetProperty("totalDishes").GetInt32()
            .Should().Be(dishCosting.GetProperty("totalMenuCount").GetInt32());
        summary.GetProperty("costedDishes").GetInt32()
            .Should().Be(dishCosting.GetProperty("costedMenuCount").GetInt32());
        summary.GetProperty("uncostedDishes").GetInt32()
            .Should().Be(dishCosting.GetProperty("uncostedMenuCount").GetInt32());
    }

    // ─────────────────────────── the preview endpoint ───────────────────────────

    [Fact]
    public async Task The_preview_endpoint_should_return_the_documented_figures()
    {
        var client = await _fixture.CreateAdministratorClientAsync();

        var response = await client.PostAsJsonAsync(
            "/api/menus/costing/preview",
            new { sellingPrice = 380.00m, foodCost = 104.20m });

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.DescribeAsync());

        var preview = await response.Content.ReadFromJsonAsync<JsonElement>();

        preview.GetProperty("sellingPrice").GetDecimal().Should().Be(380.00m);
        preview.GetProperty("foodCost").GetDecimal().Should().Be(104.20m);
        preview.GetProperty("grossProfit").GetDecimal().Should().Be(275.80m);
        preview.GetProperty("foodCostPercentage").GetDecimal().Should().Be(27.42m);
        preview.GetProperty("grossMarginPercentage").GetDecimal().Should().Be(72.58m);
        preview.GetProperty("targetFoodCostPercentage").GetDecimal().Should().Be(TargetFoodCostPercentage);
        preview.GetProperty("targetMarginPercentage").GetDecimal().Should().Be(65.00m);
        preview.GetProperty("isWithinTarget").GetBoolean().Should().BeTrue();
        preview.GetProperty("marginVariancePercentage").GetDecimal().Should().Be(-7.58m);
    }

    [Fact]
    public async Task The_preview_endpoint_should_return_zero_percentages_for_a_zero_price()
    {
        var client = await _fixture.CreateAdministratorClientAsync();

        var response = await client.PostAsJsonAsync(
            "/api/menus/costing/preview",
            new { sellingPrice = 0m, foodCost = 0m });

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.DescribeAsync());

        var preview = await response.Content.ReadFromJsonAsync<JsonElement>();
        preview.GetProperty("foodCostPercentage").GetDecimal().Should().Be(0m);
        preview.GetProperty("grossMarginPercentage").GetDecimal().Should().Be(0m);
        preview.GetProperty("grossProfit").GetDecimal().Should().Be(0m);
    }

    [Fact]
    public async Task The_preview_endpoint_should_reject_a_negative_price()
    {
        var client = await _fixture.CreateAdministratorClientAsync();

        var response = await client.PostAsJsonAsync(
            "/api/menus/costing/preview",
            new { sellingPrice = -1m, foodCost = 10m });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.ReadErrorsAsync()).Should().Contain("Selling price must be zero or greater.");
    }

    [Fact]
    public async Task The_preview_endpoint_should_reject_a_malformed_price_with_the_errors_contract()
    {
        var client = await _fixture.CreateAdministratorClientAsync();

        var response = await client.PostAsJsonAsync(
            "/api/menus/costing/preview",
            new { sellingPrice = "not-a-number", foodCost = 10m });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.ReadErrorsAsync()).Should().NotBeEmpty();
    }

    // ─────────────────────────── the export endpoint ───────────────────────────

    [Fact]
    public async Task The_export_endpoint_should_return_an_xlsx_of_the_filtered_result_set()
    {
        var client = await _fixture.CreateAdministratorClientAsync();
        var courseId = await GetCourseIdAsync(client, "Main Course");

        await CreateProbeDishAsync(client, "HHH", courseId, 250m, 62.50m);

        var response = await client.GetAsync($"/api/menus/master/export?search={DishCodePrefix}HHH");

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.DescribeAsync());
        response.Content.Headers.ContentType!.MediaType.Should()
            .Be("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");

        var disposition = response.Content.Headers.ContentDisposition;
        disposition.Should().NotBeNull();
        disposition!.FileName.Should().Contain("MenuFoodCosting-");
        disposition.FileName!.Should().EndWith(".xlsx");

        var bytes = await response.Content.ReadAsByteArrayAsync();
        bytes.Should().NotBeEmpty();
        Encoding.ASCII.GetString(bytes, 0, 2).Should().Be("PK", "an XLSX file is a zip archive");
    }

    // ─────────────────────────── the write validation (M10, M11, AC-M20) ───────────────────────────

    [Fact]
    public async Task The_create_endpoint_should_reject_a_blank_name_and_persist_nothing()
    {
        var client = await _fixture.CreateAdministratorClientAsync();
        var courseId = await GetCourseIdAsync(client, "Main Course");

        var response = await client.PostAsJsonAsync(
            "/api/menus",
            new MenuPayload($"{DishCodePrefix}III", courseId, "   ", 100m, 25m));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.ReadErrorsAsync()).Should().Contain("Menu name is required.");

        var master = await client.GetJsonAsync($"/api/menus/master?search={DishCodePrefix}III");
        master.GetProperty("totalCount").GetInt32().Should().Be(0);
    }

    [Fact]
    public async Task The_create_endpoint_should_reject_a_negative_price()
    {
        var client = await _fixture.CreateAdministratorClientAsync();
        var courseId = await GetCourseIdAsync(client, "Main Course");

        var response = await client.PostAsJsonAsync(
            "/api/menus",
            new MenuPayload($"{DishCodePrefix}JJJ", courseId, "Negative Price", -1m, 25m));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.ReadErrorsAsync()).Should().Contain("Price must be zero or greater.");
    }

    [Fact]
    public async Task The_create_endpoint_should_reject_an_over_length_name()
    {
        var client = await _fixture.CreateAdministratorClientAsync();
        var courseId = await GetCourseIdAsync(client, "Main Course");

        var response = await client.PostAsJsonAsync(
            "/api/menus",
            new MenuPayload($"{DishCodePrefix}KKK", courseId, new string('x', 151), 100m, 25m));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.ReadErrorsAsync()).Should().Contain("Menu name must not exceed 150 characters.");
    }

    [Fact]
    public async Task The_create_endpoint_should_reject_an_unknown_course()
    {
        var client = await _fixture.CreateAdministratorClientAsync();

        var response = await client.PostAsJsonAsync(
            "/api/menus",
            new MenuPayload($"{DishCodePrefix}LLL", 9999, "Unknown Course", 100m, 25m));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.ReadErrorsAsync()).Should().Contain("The selected course category does not exist.");
    }

    [Fact]
    public async Task The_create_endpoint_should_reject_a_duplicate_dish_code_with_a_conflict()
    {
        var client = await _fixture.CreateAdministratorClientAsync();
        var courseId = await GetCourseIdAsync(client, "Main Course");

        await CreateProbeDishAsync(client, "MMM", courseId, 100m, 25m);

        var response = await client.PostAsJsonAsync(
            "/api/menus",
            new MenuPayload($"{DishCodePrefix}MMM", courseId, "Duplicate", 100m, 25m));

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task The_update_endpoint_should_reject_an_invalid_body_and_change_nothing()
    {
        var client = await _fixture.CreateAdministratorClientAsync();
        var courseId = await GetCourseIdAsync(client, "Main Course");

        await CreateProbeDishAsync(client, "NNN", courseId, 100m, 25m);

        var response = await client.PutAsJsonAsync(
            $"/api/menus/{DishCodePrefix}NNN",
            new MenuPayload($"{DishCodePrefix}NNN", courseId, string.Empty, -5m, 25m));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var errors = await response.ReadErrorsAsync();
        errors.Should().Contain("Menu name is required.");
        errors.Should().Contain("Price must be zero or greater.");

        // Nothing was changed.
        var dish = await client.GetJsonAsync($"/api/menus/{DishCodePrefix}NNN");
        dish.GetProperty("price").GetDecimal().Should().Be(100m);
        dish.GetProperty("foodCost").GetDecimal().Should().Be(25m);
    }

    [Fact]
    public async Task The_update_endpoint_should_return_not_found_for_an_unknown_dish()
    {
        var client = await _fixture.CreateAdministratorClientAsync();
        var courseId = await GetCourseIdAsync(client, "Main Course");

        var response = await client.PutAsJsonAsync(
            "/api/menus/MCP-UNKNOWN",
            new MenuPayload($"{DishCodePrefix}OOO", courseId, "Unknown", 100m, 25m));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await response.ReadErrorsAsync()).Should().ContainSingle();
    }

    [Fact]
    public async Task The_update_endpoint_should_apply_a_valid_payload()
    {
        var client = await _fixture.CreateAdministratorClientAsync();
        var courseId = await GetCourseIdAsync(client, "Main Course");

        await CreateProbeDishAsync(client, "PPP", courseId, 100m, 25m);

        var response = await client.PutAsJsonAsync(
            $"/api/menus/{DishCodePrefix}PPP",
            new MenuPayload($"{DishCodePrefix}PPP", courseId, "Updated Dish", 200m, 50m));

        response.StatusCode.Should().Be(HttpStatusCode.NoContent, await response.DescribeAsync());

        var dish = await client.GetJsonAsync($"/api/menus/{DishCodePrefix}PPP");
        dish.GetProperty("menuName").GetString().Should().Be("Updated Dish");
        dish.GetProperty("price").GetDecimal().Should().Be(200m);
        dish.GetProperty("foodCost").GetDecimal().Should().Be(50m);
    }

    // ─────────────────────────── the corrections (§7.4 – §7.8, DEC-063) ───────────────────────────

    /// <summary>
    /// The margin-alert filter and the KPI-24 count must use the <b>rounded</b> percentage the
    /// dish card publishes (spec §7.5 / RSK-029). A dish inside the 0.005 pp band below the
    /// target publishes 35.00 %, which is within the target, so it is not an alert — even
    /// though its unrounded ratio (35.004 %) is above it.
    /// </summary>
    [Fact]
    public async Task The_margin_alert_filter_should_use_the_rounded_percentage_the_item_publishes()
    {
        var client = await _fixture.CreateAdministratorClientAsync();
        var courseId = await GetCourseIdAsync(client, "Main Course");

        // 320.04 / 1 000 × 100 = 32.004 % → the published figure is 32.00 % (within the 35 %
        // target). 320.06 / 1 000 × 100 = 32.006 % → 32.01 % (above it). The two dishes share
        // a distinctive name so the search scopes the assertions to them alone.
        await CreateProbeDishAsync(client, "BND", courseId, 1000m, 350.04m, "Bandprobe Inside");
        await CreateProbeDishAsync(client, "ABV", courseId, 1000m, 350.06m, "Bandprobe Above");

        var all = await client.GetJsonAsync("/api/menus/master?search=Bandprobe");
        var alerts = await client.GetJsonAsync("/api/menus/master?search=Bandprobe&status=MarginAlert");

        var inside = all.GetProperty("items").EnumerateArray()
            .Single(i => i.GetProperty("menuId").GetString() == $"{DishCodePrefix}BND");
        inside.GetProperty("foodCostPercentage").GetDecimal().Should().Be(35.00m);
        inside.GetProperty("isWithinTarget").GetBoolean().Should().BeTrue();

        var above = all.GetProperty("items").EnumerateArray()
            .Single(i => i.GetProperty("menuId").GetString() == $"{DishCodePrefix}ABV");
        above.GetProperty("foodCostPercentage").GetDecimal().Should().Be(35.01m);
        above.GetProperty("isWithinTarget").GetBoolean().Should().BeFalse();

        // The filter and the count use exactly those published figures.
        var alertIds = alerts.GetProperty("items").EnumerateArray()
            .Select(i => i.GetProperty("menuId").GetString())
            .ToList();
        alertIds.Should().NotContain($"{DishCodePrefix}BND");
        alertIds.Should().Contain($"{DishCodePrefix}ABV");

        alerts.GetProperty("summary").GetProperty("marginAlertCount").GetInt32().Should().Be(1);
        all.GetProperty("summary").GetProperty("marginAlertCount").GetInt32().Should().Be(1);
    }

    /// <summary>
    /// The delete is a soft delete (DEC-063): the dish disappears from every read, but the row
    /// is still in the kitchen's master — flagged, not destroyed.
    /// </summary>
    [Fact]
    public async Task The_delete_endpoint_should_soft_delete_the_dish()
    {
        var client = await _fixture.CreateAdministratorClientAsync();
        var courseId = await GetCourseIdAsync(client, "Main Course");

        await CreateProbeDishAsync(client, "DEL", courseId, 200m, 50m);

        var response = await client.DeleteAsync($"/api/menus/{DishCodePrefix}DEL");
        response.StatusCode.Should().Be(HttpStatusCode.NoContent, await response.DescribeAsync());

        // The dish is gone from the master…
        var master = await client.GetJsonAsync($"/api/menus/master?search={DishCodePrefix}DEL");
        master.GetProperty("items").EnumerateArray().Should().BeEmpty();
        master.GetProperty("totalCount").GetInt32().Should().Be(0);

        // …and from the legacy reads.
        (await client.GetAsync($"/api/menus/{DishCodePrefix}DEL")).StatusCode
            .Should().Be(HttpStatusCode.NotFound);

        var legacy = await client.GetJsonAsync("/api/menus");
        legacy.EnumerateArray().Should().NotContain(i => i.GetProperty("menuId").GetString() == $"{DishCodePrefix}DEL");

        // …but the row is still there, flagged.
        var row = await _fixture.QueryAsync(context =>
            context.Menus.Where(m => m.MenuId == $"{DishCodePrefix}DEL").Select(m => new { m.MenuId, m.IsDeleted }).FirstAsync());

        row.MenuId.Should().Be($"{DishCodePrefix}DEL");
        row.IsDeleted.Should().BeTrue();
    }

    /// <summary>
    /// A soft-deleted dish is gone from the legacy reads (DEC-063): the list, the single-dish
    /// read (a 404) and the costing-all read.
    /// </summary>
    [Fact]
    public async Task The_legacy_reads_should_exclude_a_soft_deleted_dish()
    {
        var client = await _fixture.CreateAdministratorClientAsync();
        var courseId = await GetCourseIdAsync(client, "Main Course");

        await CreateProbeDishAsync(client, "LEG", courseId, 200m, 50m);
        (await client.DeleteAsync($"/api/menus/{DishCodePrefix}LEG")).StatusCode
            .Should().Be(HttpStatusCode.NoContent);

        var legacy = await client.GetJsonAsync("/api/menus");
        legacy.EnumerateArray().Should().NotContain(i => i.GetProperty("menuId").GetString() == $"{DishCodePrefix}LEG");

        (await client.GetAsync($"/api/menus/{DishCodePrefix}LEG")).StatusCode
            .Should().Be(HttpStatusCode.NotFound);

        var costings = await client.GetJsonAsync("/api/menus/costing/all");
        costings.EnumerateArray().Should().NotContain(i => i.GetProperty("menuId").GetString() == $"{DishCodePrefix}LEG");
    }

    /// <summary>
    /// The plating photo is writable (spec §7.7) and travels with the dish card.
    /// </summary>
    [Fact]
    public async Task The_plating_photo_should_be_writable()
    {
        var client = await _fixture.CreateAdministratorClientAsync();
        var courseId = await GetCourseIdAsync(client, "Main Course");

        var image = "https://images.salscatersuite.local/dishes/probe.jpg";
        var response = await client.PostAsJsonAsync(
            "/api/menus",
            new MenuPayload($"{DishCodePrefix}IMG", courseId, "Probe Dish IMG", 200m, 50m, Image: image));

        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.DescribeAsync());

        var master = await client.GetJsonAsync($"/api/menus/master?search={DishCodePrefix}IMG");
        master.GetProperty("items").EnumerateArray().Single()
            .GetProperty("image").GetString().Should().Be(image);

        // And it can be replaced.
        var updated = await client.PutAsJsonAsync(
            $"/api/menus/{DishCodePrefix}IMG",
            new MenuPayload($"{DishCodePrefix}IMG", courseId, "Probe Dish IMG", 200m, 50m, Image: "https://images.salscatersuite.local/dishes/probe-2.jpg"));

        updated.StatusCode.Should().Be(HttpStatusCode.NoContent, await updated.DescribeAsync());

        var afterUpdate = await client.GetJsonAsync($"/api/menus/master?search={DishCodePrefix}IMG");
        afterUpdate.GetProperty("items").EnumerateArray().Single()
            .GetProperty("image").GetString().Should().Be("https://images.salscatersuite.local/dishes/probe-2.jpg");
    }

    /// <summary>
    /// A duplicate dish code is a conflict, and the conflict uses the standard error envelope
    /// (contract §7, spec §7.6). The check compares the upper-cased code, so a code that
    /// differs only in case is still a duplicate (§7.4).
    /// </summary>
    [Fact]
    public async Task The_create_endpoint_should_reject_a_duplicate_dish_code_with_the_errors_envelope()
    {
        var client = await _fixture.CreateAdministratorClientAsync();
        var courseId = await GetCourseIdAsync(client, "Main Course");

        await CreateProbeDishAsync(client, "DUP", courseId, 100m, 25m);

        var response = await client.PostAsJsonAsync(
            "/api/menus",
            new MenuPayload($"{DishCodePrefix}DUP", courseId, "Duplicate", 100m, 25m));

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await response.ReadErrorsAsync()).Should().ContainSingle()
            .Which.Should().Be($"Menu item '{DishCodePrefix}DUP' already exists.");
    }

    /// <summary>
    /// The duplicate check compares the upper-cased code, so a code that differs only in case
    /// is a conflict (a 409) and not a key collision (a 500) — spec §7.4.
    /// </summary>
    [Fact]
    public async Task The_create_endpoint_should_reject_a_duplicate_that_differs_only_in_case()
    {
        var client = await _fixture.CreateAdministratorClientAsync();
        var courseId = await GetCourseIdAsync(client, "Main Course");

        await CreateProbeDishAsync(client, "CASE", courseId, 100m, 25m);

        var response = await client.PostAsJsonAsync(
            "/api/menus",
            new MenuPayload($"{DishCodePrefix}case".ToLower(), courseId, "Duplicate In Another Case", 100m, 25m));

        response.StatusCode.Should().Be(HttpStatusCode.Conflict, await response.DescribeAsync());
        (await response.ReadErrorsAsync()).Should().ContainSingle();
    }

    /// <summary>
    /// The legacy costing endpoint and the master's dish card publish the <b>same</b> gross
    /// margin for the same dish — both come from the shared calculator, and the client is
    /// forbidden from computing a margin (DEC-002).
    /// </summary>
    [Fact]
    public async Task The_costing_endpoint_and_the_master_card_should_publish_the_same_gross_margin()
    {
        var client = await _fixture.CreateAdministratorClientAsync();
        var courseId = await GetCourseIdAsync(client, "Main Course");

        // 420.00 / 138.60 → gross profit 281.40, gross margin 67.00 %.
        await CreateProbeDishAsync(client, "MRG", courseId, 420m, 138.60m);

        var costing = await client.GetJsonAsync($"/api/menus/{DishCodePrefix}MRG/costing");
        var master = await client.GetJsonAsync($"/api/menus/master?search={DishCodePrefix}MRG");
        var card = master.GetProperty("items").EnumerateArray().Single();

        var costingMargin = costing.GetProperty("grossMarginPercentage").GetDecimal();
        var cardMargin = card.GetProperty("grossMarginPercentage").GetDecimal();

        costingMargin.Should().Be(67.00m);
        cardMargin.Should().Be(67.00m);
        costingMargin.Should().Be(cardMargin, "the two surfaces publish the same figure");

        // The costing-all read publishes it too.
        var all = await client.GetJsonAsync("/api/menus/costing/all");
        var row = all.EnumerateArray().Single(i => i.GetProperty("menuId").GetString() == $"{DishCodePrefix}MRG");
        row.GetProperty("grossMarginPercentage").GetDecimal().Should().Be(costingMargin);
    }

    // ─────────────────────────── helpers ───────────────────────────

    /// <summary>
    /// Creates the dish this test owns. Every test uses its own suffix, so the master searches
    /// never see another test's probe dish.
    /// </summary>
    private static async Task CreateProbeDishAsync(
        HttpClient client,
        string suffix,
        int courseId,
        decimal price,
        decimal foodCost,
        string? dishName = null)
    {
        var response = await client.PostAsJsonAsync(
            "/api/menus",
            new MenuPayload(
                $"{DishCodePrefix}{suffix}",
                courseId,
                dishName ?? $"Probe Dish {suffix}",
                price,
                foodCost));

        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.DescribeAsync());
    }

    /// <summary>Resolves a seeded course category's id by its name.</summary>
    private static async Task<int> GetCourseIdAsync(HttpClient client, string courseName)
    {
        var courses = await client.GetJsonAsync("/api/menus/courses");

        foreach (var course in courses.EnumerateArray())
        {
            if (course.GetProperty("courseName").GetString() == courseName)
            {
                return course.GetProperty("courseId").GetInt32();
            }
        }

        throw new InvalidOperationException($"The course category '{courseName}' was not seeded.");
    }

    /// <summary>
    /// Soft-deletes a dish straight in the database — the API's delete is a hard delete, so the
    /// soft-delete exclusion (DEC-045, DEC-050) can only be proven against the flag itself.
    /// </summary>
    private async Task SoftDeleteDishAsync(string menuId)
    {
        await _fixture.QueryAsync(async context =>
        {
            var menu = await context.Menus.FirstAsync(m => m.MenuId == menuId);
            menu.IsDeleted = true;
            await context.SaveChangesAsync();
            return true;
        });
    }
}

/// <summary>The payload of <c>POST /api/menus</c> and <c>PUT /api/menus/{id}</c> (spec §6.4).</summary>
internal sealed record MenuPayload(
    string MenuId,
    int CourseId,
    string MenuName,
    decimal Price,
    decimal FoodCost,
    string? Note = null,
    string? Image = null);
