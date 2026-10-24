using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using SalsCaterSuite.Domain.Entities;
using SalsCaterSuite.IntegrationTests.Infrastructure;

namespace SalsCaterSuite.IntegrationTests;

/// <summary>
/// The add-on master administration endpoints (the owner's 2026-10-24 Settings → Add-ons
/// request) against a real SQL Server (LocalDB) database.
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public class AddonMasterEndpointTests
{
    private readonly IntegrationDatabaseFixture _fixture;

    public AddonMasterEndpointTests(IntegrationDatabaseFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task The_master_can_be_listed_created_read_updated_and_soft_deleted()
    {
        var client = await _fixture.CreateAdministratorClientAsync();
        var (categoryId, departmentId) = await SeedLookupsAsync();

        var create = await client.PostAsJsonAsync(
            "/api/addons",
            new AddonPayload(
                "Probe carving station",
                1500m,
                categoryId,
                departmentId,
                "Flat",
                "event",
                true));

        create.StatusCode.Should().Be(HttpStatusCode.Created, await create.DescribeAsync());
        var created = await create.Content.ReadFromJsonAsync<JsonElement>();
        var addonId = created.GetProperty("addonId").GetInt32();
        addonId.Should().BeGreaterThan(0);

        var list = await client.GetJsonAsync("/api/addons/manage");
        var row = list.EnumerateArray().Single(item =>
            item.GetProperty("addonId").GetInt32() == addonId);
        row.GetProperty("description").GetString().Should().Be("Probe carving station");
        row.GetProperty("addonCategoryName").GetString().Should().Be("Probe live stations");
        row.GetProperty("deptName").GetString().Should().Be("Probe kitchen");
        row.GetProperty("pricingBasis").GetString().Should().Be("Flat");
        row.GetProperty("unitPrice").GetDecimal().Should().Be(1500m);
        row.GetProperty("isActive").GetBoolean().Should().BeTrue();

        var detail = await client.GetJsonAsync($"/api/addons/manage/{addonId}");
        detail.GetProperty("addonId").GetInt32().Should().Be(addonId);

        var update = await client.PutAsJsonAsync(
            $"/api/addons/{addonId}",
            new AddonPayload("Probe carving station deluxe", 1800m, categoryId, departmentId, "PerGuest", "guest", false));
        update.StatusCode.Should().Be(HttpStatusCode.NoContent, await update.DescribeAsync());

        var updated = await client.GetJsonAsync($"/api/addons/manage/{addonId}");
        updated.GetProperty("description").GetString().Should().Be("Probe carving station deluxe");
        updated.GetProperty("pricingBasis").GetString().Should().Be("PerGuest");
        updated.GetProperty("unitPrice").GetDecimal().Should().Be(1800m);
        updated.GetProperty("isActive").GetBoolean().Should().BeFalse();

        // The booking picker excludes the inactive row; the administration list keeps it.
        var publicList = await client.GetJsonAsync("/api/addons?headcount=100");
        publicList.EnumerateArray()
            .Any(item => item.GetProperty("addonId").GetInt32() == addonId)
            .Should().BeFalse();

        var deleted = await client.DeleteAsync($"/api/addons/{addonId}");
        deleted.StatusCode.Should().Be(HttpStatusCode.NoContent, await deleted.DescribeAsync());

        var afterDelete = await client.GetAsync($"/api/addons/manage/{addonId}");
        afterDelete.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var deletedAgain = await client.DeleteAsync($"/api/addons/{addonId}");
        deletedAgain.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var afterList = await client.GetJsonAsync("/api/addons/manage");
        afterList.EnumerateArray()
            .Any(item => item.GetProperty("addonId").GetInt32() == addonId)
            .Should().BeFalse();
    }

    [Fact]
    public async Task The_create_endpoint_should_reject_an_invalid_payload_and_unknown_references()
    {
        var client = await _fixture.CreateAdministratorClientAsync();

        var blank = await client.PostAsJsonAsync(
            "/api/addons",
            new AddonPayload("   ", -1m));

        blank.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var blankErrors = await blank.ReadErrorsAsync();
        blankErrors.Should().Contain("Add-on description is required.");
        blankErrors.Should().Contain("Unit price must be zero or greater.");

        var unknownCategory = await client.PostAsJsonAsync(
            "/api/addons",
            new AddonPayload("Probe unknown category", 100m, 999999));

        unknownCategory.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await unknownCategory.ReadErrorsAsync()).Should().Contain("The add-on category does not exist.");

        var unknownDepartment = await client.PostAsJsonAsync(
            "/api/addons",
            new AddonPayload("Probe unknown department", 100m, null, 999999));

        unknownDepartment.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await unknownDepartment.ReadErrorsAsync()).Should().Contain("The department does not exist.");
    }

    [Fact]
    public async Task The_department_lookup_should_return_the_live_departments()
    {
        var client = await _fixture.CreateAdministratorClientAsync();
        var (_, departmentId) = await SeedLookupsAsync("Lookup kitchen");

        var departments = await client.GetJsonAsync("/api/departments");

        departments.EnumerateArray()
            .Any(item =>
                item.GetProperty("deptId").GetInt32() == departmentId
                && item.GetProperty("deptName").GetString() == "Lookup kitchen")
            .Should().BeTrue();
    }

    private async Task<(int CategoryId, int DepartmentId)> SeedLookupsAsync(
        string departmentName = "Probe kitchen")
    {
        return await _fixture.QueryAsync(async context =>
        {
            var category = new AddonCategory
            {
                AddonCategoryName = "Probe live stations"
            };
            var department = new Department
            {
                DeptName = departmentName
            };

            context.AddonCategories.Add(category);
            context.Departments.Add(department);
            await context.SaveChangesAsync();

            return (category.AddonCategoryId, department.DeptId);
        });
    }
}

/// <summary>The body of <c>POST /api/addons</c> and <c>PUT /api/addons/{id}</c>.</summary>
internal sealed record AddonPayload(
    string Description,
    decimal UnitPrice,
    int? AddonCategoryId = null,
    int? DeptId = null,
    string? PricingBasis = null,
    string? Unit = null,
    bool IsActive = true);
