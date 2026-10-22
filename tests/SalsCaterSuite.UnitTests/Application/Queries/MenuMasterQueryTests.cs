namespace SalsCaterSuite.UnitTests.Application.Queries;

using FluentAssertions;
using SalsCaterSuite.Application.Common.Models;
using SalsCaterSuite.Application.Features.Menus.DTOs;
using SalsCaterSuite.Application.Features.Menus.Queries;
using SalsCaterSuite.Domain.Entities;
using SalsCaterSuite.UnitTests.Common.Fakes;
using Xunit;

/// <summary>
/// The Menu &amp; Food Costing Master use case (spec §6.1, §10.1 M1–M9).
/// <para>
/// The Application-layer tests drive the hand-written fake, which mirrors the EF Core
/// implementation's rules exactly; the authoritative projection is asserted against EF Core in
/// <c>MenuRepositoryTests</c> and end to end on real SQL Server by the integration
/// <c>MenuCostingEndpointTests</c>.
/// </para>
/// </summary>
public class MenuMasterQueryTests
{
    /// <summary>The fixed instant the aggregate is stamped with.</summary>
    private static readonly DateTime GeneratedAtInstant =
        new(2026, 10, 20, 9, 15, 0, DateTimeKind.Utc);

    private readonly FakeSalsCaterSuiteDataStore _store = new();
    private readonly FakeMenuRepository _menus;
    private readonly GetMenuMasterQuery _master;

    public MenuMasterQueryTests()
    {
        _menus = new FakeMenuRepository(_store);
        _master = new GetMenuMasterQuery(
            _menus,
            new FakeFoodCostBenchmarkProvider(32m),
            new FakeDateTime(GeneratedAtInstant));

        _store.CourseCategories.Add(new CourseCategory { CourseId = 1, CourseName = "Appetizer" });
        _store.CourseCategories.Add(new CourseCategory { CourseId = 2, CourseName = "Main Course" });
        _store.CourseCategories.Add(new CourseCategory { CourseId = 3, CourseName = "Dessert" });
    }

    private Menu AddMenu(
        string menuId,
        string name,
        int? courseId,
        decimal? price,
        decimal? foodCost,
        string? note = null,
        bool isDeleted = false)
    {
        var menu = new Menu
        {
            MenuId = menuId,
            MenuName = name,
            CourseId = courseId,
            Note = note,
            Price = price,
            FoodCost = foodCost,
            IsDeleted = isDeleted
        };

        _store.Menus.Add(menu);
        return menu;
    }

    private Task<Result<MenuMasterDto>> ExecuteAsync(
        string? search = null,
        int? courseId = null,
        string? status = null,
        string? sort = null,
        int page = 1,
        int pageSize = 12) =>
        _master.ExecuteAsync(search, courseId, status, sort, page, pageSize);

    // ─────────────────────────── the summary (M1–M5) ───────────────────────────

    [Fact]
    public async Task The_summary_should_describe_the_whole_filtered_set_and_not_the_page()
    {
        // Arrange — three costed dishes and one uncosted one.
        AddMenu("M1", "Roast Beef", 2, 350m, 105m);
        AddMenu("M2", "Beef Steak", 2, 300m, 90m);
        AddMenu("M3", "Lumpia", 1, 200m, 60m);
        AddMenu("M4", "Leche Flan", 3, 150m, null);

        // Act — a single-row page.
        var result = await ExecuteAsync(pageSize: 1);

        // Assert
        result.Succeeded.Should().BeTrue();
        var dto = result.Data!;
        dto.Items.Should().HaveCount(1);
        dto.TotalCount.Should().Be(4);
        dto.Summary.TotalDishes.Should().Be(4);
        dto.Summary.CostedDishes.Should().Be(3);
        dto.Summary.UncostedDishes.Should().Be(1);
        dto.Summary.CostingCoveragePercentage.Should().Be(75m);
        dto.Page.Should().Be(1);
        dto.PageSize.Should().Be(1);
        dto.TotalPages.Should().Be(4);
    }

    [Fact]
    public async Task The_average_food_cost_should_be_the_margin_population_ratio()
    {
        // Arrange — Σ FoodCost = 255, Σ Price = 850 over the margin population.
        AddMenu("M1", "Roast Beef", 2, 350m, 105m);
        AddMenu("M2", "Beef Steak", 2, 300m, 90m);
        AddMenu("M3", "Lumpia", 1, 200m, 60m);

        // Act
        var result = await ExecuteAsync();

        // Assert — 255 / 850 × 100 = 30.00 %.
        result.Data!.Summary.AverageFoodCostPercentage.Should().Be(30m);
        result.Data.Summary.AverageFoodCostHeadroomPercentage.Should().Be(2m, "32 − 30");
        result.Data.Summary.AverageSellingPrice.Should().Be(283.33m, "850 / 3");
        result.Data.Summary.TargetFoodCostPercentage.Should().Be(32m);
        result.Data.Summary.TargetMarginPercentage.Should().Be(68m);
        result.Data.Summary.IsBenchmarkConfigured.Should().BeTrue();
    }

    [Fact]
    public async Task The_average_food_cost_should_be_null_when_the_margin_population_is_empty()
    {
        // Arrange — a dish without a food cost is not in the margin population.
        AddMenu("M1", "Leche Flan", 3, 150m, null);

        // Act
        var result = await ExecuteAsync();

        // Assert
        result.Data!.Summary.AverageFoodCostPercentage.Should().BeNull();
        result.Data.Summary.AverageFoodCostHeadroomPercentage.Should().BeNull();
        result.Data.Summary.BestMarginCourse.Should().BeNull();
        result.Data.Summary.AverageSellingPrice.Should().Be(0m);
        result.Data.Summary.CostingCoveragePercentage.Should().Be(0m);
        result.Data.Summary.TotalDishes.Should().Be(1);
        result.Data.Summary.UncostedDishes.Should().Be(1);
    }

    [Fact]
    public async Task The_master_should_report_an_empty_catalogue_without_failing()
    {
        // Act
        var result = await ExecuteAsync();

        // Assert
        result.Succeeded.Should().BeTrue();
        result.Data!.Items.Should().BeEmpty();
        result.Data.TotalCount.Should().Be(0);
        result.Data.TotalPages.Should().Be(0);
        result.Data.Summary.TotalDishes.Should().Be(0);
        result.Data.Summary.CostingCoveragePercentage.Should().Be(0m);
        result.Data.Summary.AverageFoodCostPercentage.Should().BeNull();
        result.Data.Summary.BestMarginCourse.Should().BeNull();
        result.Data.Summary.MarginAlertCount.Should().Be(0);
    }

    [Fact]
    public async Task A_dish_without_a_price_or_without_a_food_cost_should_be_excluded_from_the_margin_population()
    {
        // Arrange
        AddMenu("M1", "No Price", 1, null, 50m);
        AddMenu("M2", "No Cost", 1, 100m, null);
        AddMenu("M3", "Zero Cost", 1, 100m, 0m);
        AddMenu("M4", "Costed", 1, 100m, 25m);

        // Act
        var result = await ExecuteAsync();

        // Assert
        result.Data!.Summary.TotalDishes.Should().Be(4);
        result.Data.Summary.CostedDishes.Should().Be(2, "a null food cost and a zero food cost are both uncosted");
        result.Data.Summary.UncostedDishes.Should().Be(2);
        result.Data.Summary.AverageFoodCostPercentage.Should().Be(25m, "only M4 is in the margin population");
        result.Data.Summary.AverageSellingPrice.Should().Be(100m);
    }

    [Fact]
    public async Task The_margin_alert_count_should_count_the_dishes_above_the_target()
    {
        // Arrange — the target is 32 %.
        AddMenu("M1", "Over Target", 1, 100m, 40m);   // 40 %
        AddMenu("M2", "On Target", 1, 100m, 32m);     // 32 %
        AddMenu("M3", "Under Target", 1, 100m, 20m);  // 20 %

        // Act
        var result = await ExecuteAsync();

        // Assert
        result.Data!.Summary.MarginAlertCount.Should().Be(1);
    }

    [Fact]
    public async Task The_margin_alert_filter_should_use_the_rounded_percentage_the_item_publishes()
    {
        // Arrange — the target is 32 %. One dish sits INSIDE the 0.005 pp band below the
        // target (32.004 % → the published figure is 32.00, which is within the target), and
        // one sits just above it (32.006 % → 32.01, which is not). The filter and the KPI
        // count must both use the published figure, so the first dish is not an alert even
        // though its unrounded ratio is above the target.
        AddMenu("M1", "Inside The Band", 1, 1000m, 320.04m);
        AddMenu("M2", "Above The Band", 1, 1000m, 320.06m);

        // Act
        var alerts = await ExecuteAsync(status: "MarginAlert");
        var all = await ExecuteAsync();

        // Assert — the published figures.
        var inside = all.Data!.Items.Single(i => i.MenuId == "M1");
        inside.FoodCostPercentage.Should().Be(32.00m);
        inside.IsWithinTarget.Should().BeTrue();

        var above = all.Data.Items.Single(i => i.MenuId == "M2");
        above.FoodCostPercentage.Should().Be(32.01m);
        above.IsWithinTarget.Should().BeFalse();

        // Assert — the filter and the count use exactly those figures.
        alerts.Data!.Items.Should().ContainSingle().Which.MenuId.Should().Be("M2");
        alerts.Data.Summary.MarginAlertCount.Should().Be(1);
        all.Data.Summary.MarginAlertCount.Should().Be(1);
    }

    [Fact]
    public async Task The_best_margin_course_should_be_the_course_with_the_highest_margin()
    {
        // Arrange — course 2: (350−105) + (300−90) = 455 over 650 → 70.00 %;
        // course 1: (200−60) = 140 over 200 → 70.00 % — a tie, broken by the course name.
        AddMenu("M1", "Roast Beef", 2, 350m, 105m);
        AddMenu("M2", "Beef Steak", 2, 300m, 90m);
        AddMenu("M3", "Lumpia", 1, 200m, 60m);

        // Act
        var result = await ExecuteAsync();

        // Assert
        var best = result.Data!.Summary.BestMarginCourse;
        best.Should().NotBeNull();
        best!.CourseId.Should().Be(1, "both courses are at 70 %, and the tiebreak is the course name");
        best.CourseName.Should().Be("Appetizer");
        best.GrossMarginPercentage.Should().Be(70m);
        best.AverageSellingPrice.Should().Be(200m);
        best.DishCount.Should().Be(1);
    }

    [Fact]
    public async Task The_best_margin_course_should_report_the_uncategorised_group()
    {
        // Arrange — a dish without a course, exactly as KPI-16 groups it.
        AddMenu("M1", "Bespoke Platter", null, 200m, 20m);
        AddMenu("M2", "Lumpia", 1, 200m, 100m);

        // Act
        var result = await ExecuteAsync();

        // Assert
        var best = result.Data!.Summary.BestMarginCourse;
        best.Should().NotBeNull();
        best!.CourseId.Should().BeNull();
        best.CourseName.Should().Be("Uncategorised");
        best.GrossMarginPercentage.Should().Be(90m);
        best.DishCount.Should().Be(1);
    }

    // ─────────────────────────── the filters (M7, M10) ───────────────────────────

    [Fact]
    public async Task The_search_should_match_the_dish_code_the_name_and_the_note()
    {
        // Arrange
        AddMenu("DS-104", "Prime Roast Ribeye", 2, 400m, 100m);
        AddMenu("DS-105", "Lumpia", 1, 200m, 50m, note: "Crispy spring roll");
        AddMenu("DS-106", "Sans Rival", 1, 200m, 50m);

        // Act
        var byCode = await ExecuteAsync(search: "ds-104");
        var byName = await ExecuteAsync(search: "RIBEYE");
        var byNote = await ExecuteAsync(search: "spring roll");

        // Assert
        byCode.Data!.Items.Should().ContainSingle().Which.MenuId.Should().Be("DS-104");
        byName.Data!.Items.Should().ContainSingle().Which.MenuId.Should().Be("DS-104");
        byNote.Data!.Items.Should().ContainSingle().Which.MenuId.Should().Be("DS-105");
    }

    [Fact]
    public async Task The_status_filter_should_narrow_to_costed_uncosted_and_margin_alert_dishes()
    {
        // Arrange
        AddMenu("M1", "Costed", 1, 100m, 25m);
        AddMenu("M2", "Uncosted", 1, 100m, null);
        AddMenu("M3", "Alert", 1, 100m, 40m);

        // Act
        var costed = await ExecuteAsync(status: "Costed");
        var uncosted = await ExecuteAsync(status: "uncosted");
        var alerts = await ExecuteAsync(status: "MarginAlert");
        var ignored = await ExecuteAsync(status: "banana");

        // Assert
        costed.Data!.Items.Select(i => i.MenuId).Should().BeEquivalentTo(new[] { "M1", "M3" });
        uncosted.Data!.Items.Should().ContainSingle().Which.MenuId.Should().Be("M2");
        alerts.Data!.Items.Should().ContainSingle().Which.MenuId.Should().Be("M3");
        ignored.Data!.Items.Should().HaveCount(3, "an unrecognised status is ignored, never rejected");
    }

    [Fact]
    public async Task The_course_filter_should_narrow_the_result_set()
    {
        // Arrange
        AddMenu("M1", "Lumpia", 1, 200m, 50m);
        AddMenu("M2", "Roast Beef", 2, 350m, 105m);

        // Act
        var result = await ExecuteAsync(courseId: 2);

        // Assert
        result.Data!.Items.Should().ContainSingle().Which.MenuId.Should().Be("M2");
        result.Data.Summary.TotalDishes.Should().Be(1);
    }

    [Fact]
    public async Task A_soft_deleted_dish_should_be_excluded_from_the_rows_the_summary_and_the_course_counts()
    {
        // Arrange
        AddMenu("M1", "Live", 1, 200m, 50m);
        AddMenu("M2", "Deleted", 1, 200m, 50m, isDeleted: true);

        // Act
        var result = await ExecuteAsync();

        // Assert
        result.Data!.Items.Should().ContainSingle().Which.MenuId.Should().Be("M1");
        result.Data.Summary.TotalDishes.Should().Be(1);
        result.Data.Summary.CostingCoveragePercentage.Should().Be(100m);
        result.Data.Courses.Items.Single(c => c.CourseId == 1).DishCount.Should().Be(1);
        result.Data.Courses.Total.Should().Be(1);
    }

    [Fact]
    public async Task The_course_counts_should_describe_the_scope_and_ignore_the_course_filter()
    {
        // Arrange
        AddMenu("M1", "Lumpia", 1, 200m, 50m);
        AddMenu("M2", "Roast Beef", 2, 350m, 105m);
        AddMenu("M3", "Roast Beef Two", 2, 350m, 105m);

        // Act — a course scope is selected.
        var scoped = await ExecuteAsync(courseId: 2);

        // Assert — the tab counts ignore the course filter, and every course category is listed.
        scoped.Data!.Courses.Items.Should().HaveCount(3);
        scoped.Data.Courses.Items.Single(c => c.CourseId == 1).DishCount.Should().Be(1);
        scoped.Data.Courses.Items.Single(c => c.CourseId == 2).DishCount.Should().Be(2);
        scoped.Data.Courses.Items.Single(c => c.CourseId == 3).DishCount.Should().Be(0);
        scoped.Data.Courses.Total.Should().Be(3, "the tab strip's scope is everything except the course filter");
        scoped.Data.Summary.TotalDishes.Should().Be(2, "the summary follows the active scope");
    }

    [Fact]
    public async Task The_course_counts_should_narrow_with_the_search()
    {
        // Arrange
        AddMenu("M1", "Lumpia", 1, 200m, 50m);
        AddMenu("M2", "Roast Beef", 2, 350m, 105m);

        // Act
        var searched = await ExecuteAsync(search: "Roast");

        // Assert — the rows follow the search and so do the tab counts.
        searched.Data!.Items.Should().ContainSingle().Which.MenuId.Should().Be("M2");
        searched.Data.Summary.TotalDishes.Should().Be(1);
        searched.Data.Courses.Total.Should().Be(1);
        searched.Data.Courses.Items.Single(c => c.CourseId == 1).DishCount.Should().Be(0);
        searched.Data.Courses.Items.Single(c => c.CourseId == 2).DishCount.Should().Be(1);
    }

    [Fact]
    public async Task The_course_total_should_include_the_dishes_without_a_course()
    {
        // Arrange — a dish without a course has no tab, but it is part of the scope.
        AddMenu("M1", "Bespoke Platter", null, 200m, 50m);
        AddMenu("M2", "Lumpia", 1, 200m, 50m);

        // Act
        var result = await ExecuteAsync();

        // Assert
        result.Data!.Courses.Total.Should().Be(2);
        result.Data.Courses.Items.Single(c => c.CourseId == 1).DishCount.Should().Be(1);
    }

    // ─────────────────────────── the ordering (M8) ───────────────────────────

    [Fact]
    public async Task The_default_sort_should_order_by_the_food_cost_percentage()
    {
        // Arrange — 20 %, 30 % and 40 %.
        AddMenu("M1", "Forty", 1, 100m, 40m);
        AddMenu("M2", "Twenty", 1, 100m, 20m);
        AddMenu("M3", "Thirty", 1, 100m, 30m);

        // Act
        var result = await ExecuteAsync();

        // Assert
        result.Data!.Items.Select(i => i.MenuId).Should().Equal("M2", "M3", "M1");
    }

    [Fact]
    public async Task Every_sort_should_order_as_specified()
    {
        // Arrange — three dishes with distinct food costs, prices and names.
        //   M1 "Charlie": 300 / 90 → 30 % food cost, 70 % margin
        //   M2 "Alpha":   200 / 20 → 10 % food cost, 90 % margin
        //   M3 "Bravo":   100 / 50 → 50 % food cost, 50 % margin
        AddMenu("M1", "Charlie", 1, 300m, 90m);
        AddMenu("M2", "Alpha", 1, 200m, 20m);
        AddMenu("M3", "Bravo", 1, 100m, 50m);

        // Act
        var foodCostAsc = await ExecuteAsync(sort: "FoodCostAsc");
        var foodCostDesc = await ExecuteAsync(sort: "FoodCostDesc");
        var priceDesc = await ExecuteAsync(sort: "PriceDesc");
        var priceAsc = await ExecuteAsync(sort: "PriceAsc");
        var marginDesc = await ExecuteAsync(sort: "MarginDesc");
        var nameAsc = await ExecuteAsync(sort: "NameAsc");
        var unknown = await ExecuteAsync(sort: "banana");

        // Assert
        foodCostAsc.Data!.Items.Select(i => i.MenuId).Should().Equal("M2", "M1", "M3");
        foodCostDesc.Data!.Items.Select(i => i.MenuId).Should().Equal("M3", "M1", "M2");
        priceDesc.Data!.Items.Select(i => i.MenuId).Should().Equal("M1", "M2", "M3");
        priceAsc.Data!.Items.Select(i => i.MenuId).Should().Equal("M3", "M2", "M1");
        marginDesc.Data!.Items.Select(i => i.MenuId).Should().Equal("M2", "M1", "M3");
        nameAsc.Data!.Items.Select(i => i.MenuId).Should().Equal("M2", "M3", "M1");
        unknown.Data!.Items.Select(i => i.MenuId)
            .Should().Equal(foodCostAsc.Data!.Items.Select(i => i.MenuId),
                "an unrecognised sort falls back to FoodCostAsc");
    }

    [Fact]
    public async Task Every_sort_should_carry_a_stable_tiebreak_on_the_dish_code()
    {
        // Arrange — three dishes with the same food cost percentage.
        AddMenu("M3", "Three", 1, 100m, 20m);
        AddMenu("M1", "One", 1, 100m, 20m);
        AddMenu("M2", "Two", 1, 100m, 20m);

        // Act
        var result = await ExecuteAsync(sort: "FoodCostAsc");

        // Assert
        result.Data!.Items.Select(i => i.MenuId).Should().Equal("M1", "M2", "M3");
    }

    // ─────────────────────────── the paging (M8, AC-M11) ───────────────────────────

    [Fact]
    public async Task The_paging_should_be_clamped_and_the_page_should_hold_at_most_the_page_size()
    {
        // Arrange
        for (var i = 1; i <= 15; i++)
        {
            AddMenu($"M{i:00}", $"Dish {i:00}", 1, 100m, 10m);
        }

        // Act
        var clamped = await ExecuteAsync(page: 0, pageSize: 1000);
        var second = await ExecuteAsync(page: 2, pageSize: 5);

        // Assert
        clamped.Data!.Page.Should().Be(1);
        clamped.Data.PageSize.Should().Be(100);
        clamped.Data.Items.Should().HaveCount(15);
        clamped.Data.TotalPages.Should().Be(1);

        second.Data!.Page.Should().Be(2);
        second.Data.PageSize.Should().Be(5);
        second.Data.Items.Should().HaveCount(5);
        second.Data.TotalCount.Should().Be(15);
        second.Data.TotalPages.Should().Be(3);
    }

    // ─────────────────────────── the published figures (DEC-002) ───────────────────────────

    [Fact]
    public async Task Every_published_figure_should_come_from_the_server()
    {
        // Arrange — 380 / 104.20 → 27.42 % food cost, 72.58 % margin.
        AddMenu("M1", "Preview Match", 2, 380m, 104.20m);

        // Act
        var result = await ExecuteAsync();

        // Assert
        var item = result.Data!.Items.Single();
        item.SellingPrice.Should().Be(380m);
        item.FoodCost.Should().Be(104.20m);
        item.GrossProfit.Should().Be(275.80m);
        item.FoodCostPercentage.Should().Be(27.42m);
        item.GrossMarginPercentage.Should().Be(72.58m);
        item.TargetFoodCostPercentage.Should().Be(32m);
        item.IsWithinTarget.Should().BeTrue();
        item.MarginVariancePercentage.Should().Be(-4.58m);
        item.CourseName.Should().Be("Main Course");
    }

    [Fact]
    public async Task A_dish_without_a_price_should_publish_zero_percentages()
    {
        // Arrange
        AddMenu("M1", "No Price", 2, null, 50m);

        // Act
        var result = await ExecuteAsync();

        // Assert
        var item = result.Data!.Items.Single();
        item.SellingPrice.Should().Be(0m);
        item.FoodCost.Should().Be(50m);
        item.GrossProfit.Should().Be(-50m);
        item.FoodCostPercentage.Should().Be(0m);
        item.GrossMarginPercentage.Should().Be(0m);
        item.IsWithinTarget.Should().BeTrue();
    }

    [Fact]
    public async Task The_payload_should_carry_the_server_generated_timestamp()
    {
        // Act
        var result = await ExecuteAsync();

        // Assert
        result.Data!.GeneratedAt.Should().Be(GeneratedAtInstant);
        result.Data.GeneratedAt.Kind.Should().Be(DateTimeKind.Utc);
    }

    // ─────────────────────────── the export source (M12) ───────────────────────────

    [Fact]
    public async Task The_unpaged_read_should_return_the_whole_filtered_result_set()
    {
        // Arrange — fifteen dishes.
        for (var i = 1; i <= 15; i++)
        {
            AddMenu($"M{i:00}", $"Dish {i:00}", 1, 100m, 10m);
        }

        // Act
        var result = await _master.ExecuteUnpagedAsync(null, null, null, null);

        // Assert
        result.Succeeded.Should().BeTrue();
        result.Data.Should().HaveCount(15);
    }
}
