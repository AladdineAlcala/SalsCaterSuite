namespace SalsCaterSuite.UnitTests.Application.Services;

using FluentAssertions;
using SalsCaterSuite.Application.Features.Menus.Queries;
using SalsCaterSuite.Application.Features.Menus.Services;
using SalsCaterSuite.Domain.Entities;
using SalsCaterSuite.Infrastructure.Services;
using SalsCaterSuite.UnitTests.Common.Fakes;
using Xunit;

/// <summary>
/// The rounding rule of the menu costing (spec §7.5 / RSK-029).
/// <para>
/// Every published percentage is rounded with <see cref="MidpointRounding.AwayFromZero"/> —
/// the same convention the operations dashboard uses. A half-to-even rounding in one place and
/// an away-from-zero rounding in another would let the same dish be published with two
/// different percentages, which is exactly the class of divergence DEC-025 and DEC-042 exist
/// to prevent.
/// </para>
/// <para>
/// These tests pin the <b>same</b> figure from the three publishers of a dish's margin: the
/// shared calculator, the menu-costing service and the Menu &amp; Food Costing Master's dish
/// card. The chosen prices put the percentage exactly on a <c>.005</c> midpoint, which is the
/// only place where the two rounding modes disagree.
/// </para>
/// </summary>
public class MenuCostingRoundingConsistencyTests
{
    /// <summary>
    /// 109.70 ÷ 400 × 100 = <b>27.425</b> — a midpoint. Away from zero it is 27.43;
    /// half to even it would be 27.42.
    /// </summary>
    private const decimal MidpointSellingPrice = 400m;
    private const decimal MidpointFoodCost = 109.70m;
    private const decimal ExpectedMidpointFoodCostPercentage = 27.43m;
    private const decimal ExpectedMidpointGrossMarginPercentage = 72.58m;

    /// <summary>
    /// 175.31 ÷ 200 × 100 = <b>87.655</b>, and (200 − 175.31) ÷ 200 × 100 = <b>12.345</b> — a
    /// midpoint. Away from zero the margin is 12.35; half to even it would be 12.34.
    /// </summary>
    private const decimal MarginMidpointSellingPrice = 200m;
    private const decimal MarginMidpointFoodCost = 175.31m;
    private const decimal ExpectedMarginMidpointFoodCostPercentage = 87.66m;
    private const decimal ExpectedMarginMidpointGrossMarginPercentage = 12.35m;

    private readonly FakeSalsCaterSuiteDataStore _store = new();
    private readonly FakeMenuRepository _menus;

    public MenuCostingRoundingConsistencyTests()
    {
        _menus = new FakeMenuRepository(_store);
        _store.CourseCategories.Add(new CourseCategory { CourseId = 1, CourseName = "Main Course" });
    }

    private void SeedMidpointDishes()
    {
        _store.Menus.Add(new Menu
        {
            MenuId = "MID-1",
            CourseId = 1,
            MenuName = "Midpoint Dish",
            Price = MidpointSellingPrice,
            FoodCost = MidpointFoodCost
        });

        _store.Menus.Add(new Menu
        {
            MenuId = "MID-2",
            CourseId = 1,
            MenuName = "Margin Midpoint Dish",
            Price = MarginMidpointSellingPrice,
            FoodCost = MarginMidpointFoodCost
        });
    }

    [Fact]
    public void The_calculator_should_round_a_midpoint_away_from_zero()
    {
        MenuCostingCalculator.FoodCostPercentage(MidpointSellingPrice, MidpointFoodCost)
            .Should().Be(ExpectedMidpointFoodCostPercentage);

        MenuCostingCalculator.GrossMarginPercentage(MarginMidpointSellingPrice, MarginMidpointFoodCost)
            .Should().Be(ExpectedMarginMidpointGrossMarginPercentage);
    }

    [Fact]
    public async Task The_costing_service_and_the_master_card_should_publish_the_same_midpoint_figures()
    {
        // Arrange
        SeedMidpointDishes();

        var service = new MenuCostingService(_menus);
        var master = new GetMenuMasterQuery(
            _menus,
            new FakeFoodCostBenchmarkProvider(35m),
            new FakeDateTime(new DateTime(2026, 10, 20, 9, 15, 0, DateTimeKind.Utc)));

        // Act
        var serviceCosting = await service.CalculateCostingAsync("MID-1");
        var serviceMargin = await service.CalculateCostingAsync("MID-2");
        var masterResult = await master.ExecuteAsync(search: "MID", courseId: null, status: null, sort: null, page: 1, pageSize: 12);

        // Assert — the calculator, the costing service and the dish card agree to the penny.
        var card = masterResult.Data!.Items.Single(i => i.MenuId == "MID-1");
        var marginCard = masterResult.Data.Items.Single(i => i.MenuId == "MID-2");

        serviceCosting.Data!.FoodCostPercentage.Should().Be(ExpectedMidpointFoodCostPercentage);
        card.FoodCostPercentage.Should().Be(ExpectedMidpointFoodCostPercentage);
        card.FoodCostPercentage.Should().Be(serviceCosting.Data.FoodCostPercentage);
        MenuCostingCalculator.FoodCostPercentage(card.SellingPrice, card.FoodCost)
            .Should().Be(card.FoodCostPercentage);

        card.GrossMarginPercentage.Should().Be(ExpectedMidpointGrossMarginPercentage);

        serviceMargin.Data!.FoodCostPercentage.Should().Be(ExpectedMarginMidpointFoodCostPercentage);
        marginCard.FoodCostPercentage.Should().Be(ExpectedMarginMidpointFoodCostPercentage);
        marginCard.GrossMarginPercentage.Should().Be(ExpectedMarginMidpointGrossMarginPercentage);
        MenuCostingCalculator.GrossMarginPercentage(marginCard.SellingPrice, marginCard.FoodCost)
            .Should().Be(marginCard.GrossMarginPercentage);
    }
}
