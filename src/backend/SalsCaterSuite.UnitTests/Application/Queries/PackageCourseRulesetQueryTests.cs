namespace SalsCaterSuite.UnitTests.Application.Queries;

using FluentAssertions;
using SalsCaterSuite.Application.Features.Packages.Queries;
using SalsCaterSuite.Domain.Entities;
using SalsCaterSuite.UnitTests.Common.Fakes;
using Xunit;

/// <summary>
/// The course-ruleset read (contract §13.1, REQ-PKG-02) over a seeded population.
/// </summary>
public class PackageCourseRulesetQueryTests
{
    private readonly FakeSalsCaterSuiteDataStore _store = new();
    private readonly GetPackageCourseRulesetQuery _query;

    public PackageCourseRulesetQueryTests()
    {
        _store.Packages.Add(new Package
        {
            PackageId = 1,
            PackageType = "Wedding",
            Description = "Wedding Grand Tier",
            AmountPax = 1450m,
            MinPax = 150,
            IsActive = true,
            NumPaxId = 4,
        });

        _store.CourseCategories.Add(new CourseCategory { CourseId = 1, CourseName = "Main Course" });
        _store.CourseCategories.Add(new CourseCategory { CourseId = 2, CourseName = "Dessert" });

        _store.Menus.Add(new Menu { MenuId = "DS-1", CourseId = 1, MenuName = "Beef", Price = 500m, FoodCost = 200m });
        _store.Menus.Add(new Menu { MenuId = "DS-2", CourseId = 1, MenuName = "Chicken", Price = 400m, FoodCost = 100m });
        _store.Menus.Add(new Menu { MenuId = "DS-3", CourseId = 1, MenuName = "Uncosted", Price = 300m, FoodCost = 0m });
        _store.Menus.Add(new Menu { MenuId = "DS-4", CourseId = 2, MenuName = "Cake", Price = 200m, FoodCost = 50m });

        _store.PackageCourseRules.Add(new PackageCourseRule
        {
            PackageCourseRuleId = 1,
            PackageId = 1,
            CourseId = 1,
            RequiredQuota = 1,
            RuleDescription = "One beef selection"
        });

        _query = new GetPackageCourseRulesetQuery(
            new FakePackageRepository(_store),
            new FakePackageCourseRuleRepository(_store),
            new FakeMenuRepository(_store),
            new FakeFoodCostBenchmarkProvider(35m),
            new FakeDateTime(new DateTime(2026, 10, 22, 9, 0, 0, DateTimeKind.Utc)));
    }

    [Fact]
    public async Task The_ruleset_should_publish_the_tier_identity_and_its_planning_figures()
    {
        var result = await _query.ExecuteAsync(1);

        result.Succeeded.Should().BeTrue();

        var ruleset = result.Data!;
        ruleset.PackageId.Should().Be(1);
        ruleset.PackageName.Should().Be("Wedding Grand Tier");
        ruleset.PackageType.Should().Be("Wedding");
        ruleset.AmountPax.Should().Be(1450m);
        ruleset.MinPax.Should().Be(150);
        ruleset.IsActive.Should().BeTrue();
        ruleset.TargetFoodCostPercentage.Should().Be(35m);
        ruleset.TargetMarginPercentage.Should().Be(65m);
        ruleset.IsBenchmarkConfigured.Should().BeTrue();

        // 1450 × 35 % = 507.50 (BR-14).
        ruleset.CostCeilingPerPax.Should().Be(507.50m);

        // 1450 × 150 = 217 500.00 (BR-16).
        ruleset.MinimumContractValue.Should().Be(217500.00m);

        ruleset.GeneratedAt.Should().Be(new DateTime(2026, 10, 22, 9, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public async Task The_qualified_pool_should_be_the_live_dishes_of_the_rule_course()
    {
        var ruleset = (await _query.ExecuteAsync(1)).Data!;

        var rule = ruleset.Rules.Should().ContainSingle().Subject;
        rule.CourseId.Should().Be(1);
        rule.CourseName.Should().Be("Main Course");
        rule.RequiredQuota.Should().Be(1);
        rule.PoolSize.Should().Be(3);

        // A dish is costed when its food cost is greater than zero (DEC-087).
        rule.PoolCostedCount.Should().Be(2);

        // (200 + 100) ÷ 2 = 150.00.
        rule.PoolAverageFoodCost.Should().Be(150.00m);
        rule.Pool.Should().HaveCount(3);
    }

    [Fact]
    public async Task The_standard_run_should_be_the_mean_of_the_costed_pool()
    {
        var ruleset = (await _query.ExecuteAsync(1)).Data!;

        // The only rule's pool averages 150.00 → the standard run is 150.00 (DEC-083).
        ruleset.StandardRunFoodCostPerPax.Should().Be(150.00m);
        ruleset.StandardRunFoodCostPercentage.Should().Be(10.34m);
        ruleset.BaseGrossProfitPercentage.Should().Be(89.66m);
    }

    [Fact]
    public async Task A_tier_without_a_rule_should_publish_no_standard_run()
    {
        _store.PackageCourseRules.Clear();

        var ruleset = (await _query.ExecuteAsync(1)).Data!;

        ruleset.Rules.Should().BeEmpty();
        ruleset.StandardRunFoodCostPerPax.Should().BeNull();
        ruleset.StandardRunFoodCostPercentage.Should().BeNull();
        ruleset.BaseGrossProfitPercentage.Should().BeNull();
    }

    [Fact]
    public async Task A_rule_whose_pool_holds_no_costed_dish_should_publish_no_standard_run()
    {
        _store.Menus.ForEach(menu => menu.FoodCost = 0m);

        var ruleset = (await _query.ExecuteAsync(1)).Data!;

        ruleset.StandardRunFoodCostPerPax.Should().BeNull();
    }

    [Fact]
    public async Task The_rules_should_be_ordered_by_course_name()
    {
        _store.PackageCourseRules.Add(new PackageCourseRule
        {
            PackageCourseRuleId = 2,
            PackageId = 1,
            CourseId = 2,
            RequiredQuota = 1,
            RuleDescription = "One dessert"
        });

        var ruleset = (await _query.ExecuteAsync(1)).Data!;

        ruleset.Rules.Select(rule => rule.CourseName).Should().ContainInOrder("Dessert", "Main Course");
    }

    [Fact]
    public async Task A_soft_deleted_tier_should_not_be_found()
    {
        _store.Packages[0].IsDeleted = true;

        var result = await _query.ExecuteAsync(1);

        result.Succeeded.Should().BeFalse();
        result.Errors.Should().ContainSingle().Which.Should().Be("Package not found.");
    }

    [Fact]
    public async Task A_soft_deleted_rule_should_not_be_published()
    {
        _store.PackageCourseRules[0].IsDeleted = true;

        var ruleset = (await _query.ExecuteAsync(1)).Data!;

        ruleset.Rules.Should().BeEmpty();
    }
}
