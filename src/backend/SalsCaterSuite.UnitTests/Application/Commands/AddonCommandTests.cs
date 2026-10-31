namespace SalsCaterSuite.UnitTests.Application.Commands;

using FluentAssertions;
using SalsCaterSuite.Application.Features.Addons;
using SalsCaterSuite.Application.Features.Addons.Commands;
using SalsCaterSuite.Application.Features.Addons.DTOs.Request;
using SalsCaterSuite.Application.Features.Addons.DTOs.Response;
using SalsCaterSuite.Application.Features.Addons.Queries;
using SalsCaterSuite.Application.Features.Addons.Validators;
using SalsCaterSuite.Domain.Entities;
using SalsCaterSuite.Domain.Enums;
using SalsCaterSuite.UnitTests.Common.Fakes;
using Xunit;

/// <summary>
/// The add-on master's administration use cases (the owner's 2026-10-24 Settings → Add-ons
/// request), exercised through the hand-written repositories (DEC-037) — no EF Core.
/// </summary>
public class AddonCommandTests
{
    private readonly FakeSalsCaterSuiteDataStore _store = new();
    private readonly FakeAddonRepository _addons;
    private readonly FakeDepartmentRepository _departments;
    private readonly CreateAddonCommand _create;
    private readonly UpdateAddonCommand _update;
    private readonly DeleteAddonCommand _delete;
    private readonly GetAddonManagementListQuery _list;
    private readonly GetAddonManagementDetailQuery _detail;
    private readonly GetDepartmentsQuery _departmentsQuery;

    public AddonCommandTests()
    {
        _addons = new FakeAddonRepository(_store);
        _departments = new FakeDepartmentRepository(_store);
        var validator = new AddonUpsertValidator();
        _create = new CreateAddonCommand(_addons, _departments, validator);
        _update = new UpdateAddonCommand(_addons, _departments, validator);
        _delete = new DeleteAddonCommand(_addons);
        _list = new GetAddonManagementListQuery(_addons);
        _detail = new GetAddonManagementDetailQuery(_addons);
        _departmentsQuery = new GetDepartmentsQuery(_departments);

        _store.AddonCategories.Add(new AddonCategory
        {
            AddonCategoryId = 2,
            AddonCategoryName = "Live stations"
        });
        _store.Departments.Add(new Department
        {
            DeptId = 3,
            DeptName = "Kitchen"
        });
    }

    private static AddonUpsertRequest Request() => new()
    {
        AddonCategoryId = 2,
        DeptId = 3,
        PricingBasis = AddonPricingBasis.PerGuest,
        Description = "Dessert bar",
        UnitPrice = 150m,
        Unit = "guest",
        IsActive = true
    };

    [Fact]
    public async Task Create_should_store_the_addon_and_return_its_identity()
    {
        var result = await _create.ExecuteAsync(Request());

        result.Succeeded.Should().BeTrue();
        result.Data.Should().BeGreaterThan(0);
        _store.AddonDetails.Should().ContainSingle();
        var addon = _store.AddonDetails[0];
        addon.Description.Should().Be("Dessert bar");
        addon.AddonCategoryId.Should().Be(2);
        addon.DeptId.Should().Be(3);
        addon.PricingBasis.Should().Be(AddonPricingBasis.PerGuest);
        addon.UnitPrice.Should().Be(150m);
        addon.Unit.Should().Be("guest");
        addon.IsActive.Should().BeTrue();
        addon.IsDeleted.Should().BeFalse();
    }

    [Fact]
    public async Task Create_should_reject_an_invalid_payload_and_persist_nothing()
    {
        var result = await _create.ExecuteAsync(new AddonUpsertRequest
        {
            Description = "   ",
            UnitPrice = -1m
        });

        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain("Add-on description is required.");
        result.Errors.Should().Contain("Unit price must be zero or greater.");
        _store.AddonDetails.Should().BeEmpty();
    }

    [Fact]
    public async Task Create_should_reject_an_unknown_category()
    {
        var result = await _create.ExecuteAsync(Request() with { AddonCategoryId = 999 });

        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain("The add-on category does not exist.");
        _store.AddonDetails.Should().BeEmpty();
    }

    [Fact]
    public async Task Create_should_reject_an_unknown_department()
    {
        var result = await _create.ExecuteAsync(Request() with { DeptId = 999 });

        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain("The department does not exist.");
        _store.AddonDetails.Should().BeEmpty();
    }

    [Fact]
    public async Task Update_should_apply_every_editable_field()
    {
        var created = await _create.ExecuteAsync(Request());

        var result = await _update.ExecuteAsync(created.Data, new AddonUpsertRequest
        {
            AddonCategoryId = 2,
            DeptId = 3,
            PricingBasis = AddonPricingBasis.Flat,
            Description = "Carving station",
            UnitPrice = 1500m,
            Unit = "event",
            IsActive = false
        });

        result.Succeeded.Should().BeTrue();
        var addon = _store.AddonDetails.Single(row => row.AddonId == created.Data);
        addon.Description.Should().Be("Carving station");
        addon.PricingBasis.Should().Be(AddonPricingBasis.Flat);
        addon.UnitPrice.Should().Be(1500m);
        addon.Unit.Should().Be("event");
        addon.IsActive.Should().BeFalse();
        addon.UpdatedDate.Should().NotBeNull();
    }

    [Fact]
    public async Task Update_should_reject_an_invalid_payload_before_reading_the_row()
    {
        var result = await _update.ExecuteAsync(999, new AddonUpsertRequest
        {
            Description = string.Empty
        });

        result.Succeeded.Should().BeFalse();
        result.Should().NotBeOfType<AddonNotFoundResult>();
        result.Errors.Should().Contain("Add-on description is required.");
    }

    [Fact]
    public async Task Update_should_report_a_missing_addon_as_not_found()
    {
        var result = await _update.ExecuteAsync(999, Request());

        result.Succeeded.Should().BeFalse();
        result.Should().BeOfType<AddonNotFoundResult>();
    }

    [Fact]
    public async Task Delete_should_soft_delete_and_a_second_delete_should_be_not_found()
    {
        var created = await _create.ExecuteAsync(Request());

        var first = await _delete.ExecuteAsync(created.Data);
        var second = await _delete.ExecuteAsync(created.Data);

        first.Succeeded.Should().BeTrue();
        _store.AddonDetails.Single().IsDeleted.Should().BeTrue();
        second.Succeeded.Should().BeFalse();
        second.Should().BeOfType<AddonNotFoundResult>();
    }

    [Fact]
    public async Task The_management_list_should_include_inactive_rows_and_exclude_deleted_ones()
    {
        await _create.ExecuteAsync(Request());
        await _create.ExecuteAsync(Request() with { Description = "Inactive bar", IsActive = false });
        _store.AddonDetails.Add(new AddonDetail
        {
            AddonId = 50,
            Description = "Deleted bar",
            IsDeleted = true
        });

        var result = await _list.ExecuteAsync();

        result.Succeeded.Should().BeTrue();
        result.Data!.Select(row => row.Description)
            .Should().BeEquivalentTo(new[] { "Dessert bar", "Inactive bar" });
        result.Data!.Single(row => row.Description == "Dessert bar").AddonCategoryName
            .Should().Be("Live stations");
        result.Data!.Single(row => row.Description == "Dessert bar").DeptName
            .Should().Be("Kitchen");
    }

    [Fact]
    public async Task The_detail_read_should_report_a_missing_addon_as_not_found()
    {
        var result = await _detail.ExecuteAsync(999);

        result.Succeeded.Should().BeFalse();
        result.Should().BeOfType<AddonNotFoundResult<AddonAdminItemResponse>>();
    }

    [Fact]
    public async Task The_department_lookup_should_return_the_live_departments()
    {
        var result = await _departmentsQuery.ExecuteAsync();

        result.Succeeded.Should().BeTrue();
        result.Data.Should().ContainSingle(row => row.DeptId == 3 && row.DeptName == "Kitchen");
    }
}
