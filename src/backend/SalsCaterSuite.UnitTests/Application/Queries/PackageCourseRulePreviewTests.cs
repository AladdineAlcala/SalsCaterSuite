namespace SalsCaterSuite.UnitTests.Application.Queries;

using FluentAssertions;
using SalsCaterSuite.Application.Features.Packages.DTOs;
using SalsCaterSuite.Application.Features.Packages.Queries;
using SalsCaterSuite.Domain.Entities;
using SalsCaterSuite.UnitTests.Common.Fakes;
using Xunit;

/// <summary>
/// The selection simulator and the headcount forecast (contract §13.3, BR-15, DEC-080, DEC-087).
/// </summary>
public class PackageCourseRulePreviewTests
{
    private readonly FakeSalsCaterSuiteDataStore _store = new();
    private readonly PreviewPackageCourseRuleQuery _query;

    public PackageCourseRulePreviewTests()
    {
        _store.Packages.Add(new Package
        {
            PackageId = 1,
            PackageType = "Wedding",
            Description = "Wedding Grand Tier",
            AmountPax = 1000m,
            MinPax = 50,
            IsActive = true
        });

        _store.CourseCategories.Add(new CourseCategory { CourseId = 1, CourseName = "Main Course" });
        _store.CourseCategories.Add(new CourseCategory { CourseId = 2, CourseName = "Dessert" });

        _store.Menus.Add(new Menu { MenuId = "DS-1", CourseId = 1, MenuName = "Beef", Price = 500m, FoodCost = 100m });
        _store.Menus.Add(new Menu { MenuId = "DS-2", CourseId = 1, MenuName = "Pork", Price = 500m, FoodCost = 400m });
        _store.Menus.Add(new Menu { MenuId = "DS-3", CourseId = 1, MenuName = "Uncosted", Price = 300m, FoodCost = 0m });
        _store.Menus.Add(new Menu { MenuId = "DS-4", CourseId = 2, MenuName = "Cake", Price = 200m, FoodCost = 50m });

        _store.PackageCourseRules.Add(new PackageCourseRule
        {
            PackageCourseRuleId = 1,
            PackageId = 1,
            CourseId = 1,
            RequiredQuota = 1,
            RuleDescription = "Exactly one main course"
        });

        _query = new PreviewPackageCourseRuleQuery(
            new GetPackageCourseRulesetQuery(
                new FakePackageRepository(_store),
                new FakePackageCourseRuleRepository(_store),
                new FakeMenuRepository(_store),
                new FakeFoodCostBenchmarkProvider(35m),
                new FakeDateTime(new DateTime(2026, 10, 22, 9, 0, 0, DateTimeKind.Utc))),
            new FakeMenuRepository(_store),
            new SalsCaterSuite.Application.Features.Packages.Validators.PackageCourseRulePreviewValidator(
                new FakeMenuRepository(_store)),
            new FakeDateTime(new DateTime(2026, 10, 22, 9, 0, 0, DateTimeKind.Utc)));
    }

    private static PackageCourseRulePreviewRequest Request(params PackageCourseSelectionDto[] selections) =>
        new(selections);

    [Fact]
    public async Task A_satisfied_selection_should_be_compliant()
    {
        var preview = (await _query.ExecuteAsync(1, Request(new PackageCourseSelectionDto(1, "DS-1")))).Data!;

        preview.SelectedFoodCostPerPax.Should().Be(100.00m);
        preview.IsCostKnown.Should().BeTrue();
        preview.IsQuotaCompliant.Should().BeTrue();
        preview.IsCompliant.Should().BeTrue();
        preview.Compliance.Should().ContainSingle();
        preview.Compliance[0].SelectedCount.Should().Be(1);
        preview.Compliance[0].RequiredQuota.Should().Be(1);
        preview.Compliance[0].HasRule.Should().BeTrue();
    }

    [Fact]
    public async Task A_short_selection_should_not_be_compliant()
    {
        // The rule demands exactly one dish and none was selected (BR-15).
        var preview = (await _query.ExecuteAsync(1, Request())).Data!;

        preview.IsQuotaCompliant.Should().BeFalse();
        preview.IsCompliant.Should().BeFalse();
        preview.Compliance[0].SelectedCount.Should().Be(0);
    }

    [Fact]
    public async Task An_over_selection_should_not_be_compliant()
    {
        // A quota of one is not satisfied by two dishes.
        var preview = (await _query.ExecuteAsync(1, Request(
            new PackageCourseSelectionDto(1, "DS-1"),
            new PackageCourseSelectionDto(1, "DS-2")))).Data!;

        preview.IsQuotaCompliant.Should().BeFalse();
        preview.Compliance[0].SelectedCount.Should().Be(2);
    }

    [Fact]
    public async Task A_selection_naming_an_unruled_course_should_not_be_compliant()
    {
        var preview = (await _query.ExecuteAsync(1, Request(new PackageCourseSelectionDto(2, "DS-4")))).Data!;

        preview.IsQuotaCompliant.Should().BeFalse();
        preview.IsCompliant.Should().BeFalse();

        var unruled = preview.Compliance.Should().ContainSingle(entry => !entry.HasRule).Subject;
        unruled.CourseId.Should().Be(2);
        unruled.CourseName.Should().Be("Dessert");
        unruled.SelectedCount.Should().Be(1);
        unruled.IsCompliant.Should().BeFalse();
    }

    [Fact]
    public async Task A_ceiling_breach_should_warn_without_blocking_the_quota_verdict()
    {
        // The ceiling is 1000 × 35 % = 350.00 and the selected dish costs 400.00 (DEC-080).
        var preview = (await _query.ExecuteAsync(1, Request(new PackageCourseSelectionDto(1, "DS-2")))).Data!;

        preview.CostCeilingPerPax.Should().Be(350.00m);
        preview.IsCeilingBreached.Should().BeTrue();
        preview.CeilingShortfallPerPax.Should().Be(50.00m);

        // 50 ÷ 350 × 100 = 14.29 %.
        preview.CeilingOveragePercentage.Should().Be(14.29m);
        preview.EffectiveFoodCostPercentage.Should().Be(40.00m);

        // The quota verdict is published separately and is unaffected by the breach.
        preview.IsQuotaCompliant.Should().BeTrue();
        preview.IsCompliant.Should().BeFalse();
    }

    [Fact]
    public async Task An_uncosted_selected_dish_should_make_the_food_cost_unknown()
    {
        var preview = (await _query.ExecuteAsync(1, Request(new PackageCourseSelectionDto(1, "DS-3")))).Data!;

        // Unknown — never zero (DEC-087).
        preview.SelectedFoodCostPerPax.Should().BeNull();
        preview.FoodCostPerPax.Should().BeNull();
        preview.IsCostKnown.Should().BeFalse();
        preview.IsCeilingBreached.Should().BeFalse();

        // The quota verdict still counts the dish.
        preview.Compliance[0].SelectedCount.Should().Be(1);
        preview.IsQuotaCompliant.Should().BeTrue();
    }

    [Fact]
    public async Task The_stress_preview_should_multiply_the_selected_cost()
    {
        var request = new PackageCourseRulePreviewRequest(
            new[] { new PackageCourseSelectionDto(1, "DS-1") },
            10m);

        var preview = (await _query.ExecuteAsync(1, request)).Data!;

        preview.StressPercentage.Should().Be(10m);
        preview.StressedFoodCostPerPax.Should().Be(110.00m);
        preview.StressedFoodCostPercentage.Should().Be(11.00m);
    }

    [Fact]
    public async Task The_headcount_should_be_the_requested_one_otherwise_the_tier_floor()
    {
        var requested = new PackageCourseRulePreviewRequest(
            new[] { new PackageCourseSelectionDto(1, "DS-1") },
            null,
            120);

        var preview = (await _query.ExecuteAsync(1, requested)).Data!;

        preview.Pax.Should().Be(120);
        preview.GrossRevenue.Should().Be(120000.00m);
        preview.FoodCostTotal.Should().Be(12000.00m);
        preview.GrossProfit.Should().Be(108000.00m);
        preview.GrossProfitPercentage.Should().Be(90.00m);
    }

    [Fact]
    public async Task The_headcount_should_fall_back_to_the_tier_floor()
    {
        var preview = (await _query.ExecuteAsync(1, Request(new PackageCourseSelectionDto(1, "DS-1")))).Data!;

        preview.Pax.Should().Be(50);
        preview.GrossRevenue.Should().Be(50000.00m);
        preview.FoodCostTotal.Should().Be(5000.00m);
        preview.GrossProfit.Should().Be(45000.00m);
        preview.GrossProfitPercentage.Should().Be(90.00m);
    }

    [Fact]
    public async Task An_unknown_dish_should_be_rejected()
    {
        var result = await _query.ExecuteAsync(1, Request(new PackageCourseSelectionDto(1, "DS-999")));

        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain("Dish DS-999 does not exist.");
    }

    [Fact]
    public async Task A_dish_of_another_course_should_be_rejected()
    {
        var result = await _query.ExecuteAsync(1, Request(new PackageCourseSelectionDto(1, "DS-4")));

        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain("Dish DS-4 does not belong to course 1.");
    }

    [Fact]
    public async Task A_repeated_dish_should_be_rejected()
    {
        var result = await _query.ExecuteAsync(1, Request(
            new PackageCourseSelectionDto(1, "DS-1"),
            new PackageCourseSelectionDto(1, "DS-1")));

        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain("Dish DS-1 is selected more than once.");
    }

    [Fact]
    public async Task A_null_selection_member_should_be_rejected()
    {
        var result = await _query.ExecuteAsync(1, new PackageCourseRulePreviewRequest(null));

        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain("Selections must be supplied.");
    }

    [Fact]
    public async Task A_soft_deleted_tier_should_not_be_found()
    {
        _store.Packages[0].IsDeleted = true;

        var result = await _query.ExecuteAsync(1, Request(new PackageCourseSelectionDto(1, "DS-1")));

        result.Succeeded.Should().BeFalse();
        result.Errors.Should().ContainSingle().Which.Should().Be("Package not found.");
    }
}
