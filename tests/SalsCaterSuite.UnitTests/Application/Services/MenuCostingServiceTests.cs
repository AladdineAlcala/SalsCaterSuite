namespace SalsCaterSuite.UnitTests.Application.Services;

using FluentAssertions;
using SalsCaterSuite.Domain.Entities;
using SalsCaterSuite.Infrastructure.Services;
using SalsCaterSuite.UnitTests.Common.Fakes;
using Xunit;

/// <summary>
/// DEC-037: the menu-costing service now reads through <see cref="IMenuRepository"/>, so
/// these Application-level tests drive a hand-written fake instead of EF Core. The costing
/// arithmetic assertions are unchanged; the EF-specific read behaviour is covered by
/// <c>MenuRepositoryTests</c> (Infrastructure, EF InMemory).
/// </summary>
public class MenuCostingServiceTests
{
    private readonly FakeSalsCaterSuiteDataStore _store = new();
    private readonly MenuCostingService _service;

    public MenuCostingServiceTests()
    {
        _service = new MenuCostingService(new FakeMenuRepository(_store));
    }

    [Fact]
    public async Task CalculateCostingAsync_WhenMenuExists_ShouldReturnAccurateCosting()
    {
        // Arrange
        _store.Menus.Add(new Menu
        {
            MenuId = "M001",
            MenuName = "Beef Stroganoff",
            Price = 350.00m,
            FoodCost = 105.00m
        });

        // Act
        var result = await _service.CalculateCostingAsync("M001");

        // Assert
        result.Succeeded.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.MenuId.Should().Be("M001");
        result.Data.MenuName.Should().Be("Beef Stroganoff");
        result.Data.SellingPrice.Should().Be(350.00m);
        result.Data.FoodCost.Should().Be(105.00m);
        result.Data.GrossProfit.Should().Be(245.00m);
        result.Data.FoodCostPercentage.Should().Be(30.00m); // (105 / 350) * 100 = 30.00%
        result.Data.GrossMarginPercentage.Should().Be(70.00m); // (245 / 350) * 100 = 70.00%
        result.Data.TargetFoodCostPercentage.Should().Be(35.0m);
    }

    [Fact]
    public async Task CalculateCostingAsync_WhenMenuNotFound_ShouldReturnFailure()
    {
        // Act
        var result = await _service.CalculateCostingAsync("NON_EXISTENT");

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain("Menu dish not found.");
        result.Data.Should().BeNull();
    }

    [Fact]
    public async Task CalculateCostingAsync_WhenSellingPriceIsZero_ShouldHandleGracefullyWithoutDivisionByZero()
    {
        // Arrange
        _store.Menus.Add(new Menu
        {
            MenuId = "M002",
            MenuName = "Complimentary Salad",
            Price = 0m,
            FoodCost = 30m
        });

        // Act
        var result = await _service.CalculateCostingAsync("M002");

        // Assert
        result.Succeeded.Should().BeTrue();
        result.Data!.FoodCostPercentage.Should().Be(0m);
        result.Data.GrossMarginPercentage.Should().Be(0m, "a dish without a selling price implies no margin");
        result.Data.GrossProfit.Should().Be(-30m);
    }

    [Fact]
    public async Task GetAllCostingsAsync_ShouldReturnOrderedListOfCostings()
    {
        // Arrange
        _store.Menus.AddRange(
            new Menu { MenuId = "M2", MenuName = "Zesty Chicken", Price = 200m, FoodCost = 60m },
            new Menu { MenuId = "M1", MenuName = "Apple Glazed Pork", Price = 300m, FoodCost = 90m });

        // Act
        var result = await _service.GetAllCostingsAsync();

        // Assert
        result.Succeeded.Should().BeTrue();
        result.Data.Should().HaveCount(2);
        result.Data![0].MenuName.Should().Be("Apple Glazed Pork");
        result.Data[1].MenuName.Should().Be("Zesty Chicken");
    }
}
