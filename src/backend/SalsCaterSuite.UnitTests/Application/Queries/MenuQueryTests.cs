namespace SalsCaterSuite.UnitTests.Application.Queries;

using FluentAssertions;
using SalsCaterSuite.Application.Features.Menus.DTOs.Request;
using SalsCaterSuite.Application.Features.Menus.DTOs.Response;
using SalsCaterSuite.Application.Features.Menus.Models;
using SalsCaterSuite.Application.Features.Menus.Queries;
using SalsCaterSuite.Domain.Entities;
using SalsCaterSuite.UnitTests.Common.Fakes;
using Xunit;

/// <summary>DEC-037: the menu read use cases are repository-backed (hand-written fake).</summary>
public class MenuQueryTests
{
    private readonly FakeSalsCaterSuiteDataStore _store = new();
    private readonly FakeMenuRepository _menus;
    private readonly GetMenusQuery _list;
    private readonly GetMenuByIdQuery _byId;
    private readonly GetCoursesQuery _courses;

    public MenuQueryTests()
    {
        _menus = new FakeMenuRepository(_store);
        _list = new GetMenusQuery(_menus);
        _byId = new GetMenuByIdQuery(_menus);
        _courses = new GetCoursesQuery(_menus);
    }

    [Fact]
    public async Task GetMenusQuery_ShouldReturnMenusOrderedByCourseThenNameWithCourseName()
    {
        // Arrange
        _store.CourseCategories.Add(new CourseCategory { CourseId = 1, CourseName = "Main Course" });
        _store.Menus.Add(new Menu { MenuId = "M2", CourseId = 1, MenuName = "Zesty Chicken", Price = 200m, FoodCost = 60m });
        _store.Menus.Add(new Menu { MenuId = "M1", CourseId = 1, MenuName = "Apple Glazed Pork", Price = 300m, FoodCost = 90m });

        // Act
        var result = await _list.ExecuteAsync(null);

        // Assert
        result.Succeeded.Should().BeTrue();
        result.Data.Should().HaveCount(2);
        result.Data![0].MenuName.Should().Be("Apple Glazed Pork");
        result.Data[0].CourseName.Should().Be("Main Course");
        result.Data[1].MenuName.Should().Be("Zesty Chicken");
    }

    [Fact]
    public async Task GetMenusQuery_WhenCourseFilterSupplied_ShouldReturnOnlyThatCourse()
    {
        // Arrange
        _store.Menus.Add(new Menu { MenuId = "M1", CourseId = 1, MenuName = "Soup" });
        _store.Menus.Add(new Menu { MenuId = "M2", CourseId = 2, MenuName = "Cake" });

        // Act
        var result = await _list.ExecuteAsync(2);

        // Assert
        result.Succeeded.Should().BeTrue();
        result.Data.Should().ContainSingle().Which.MenuId.Should().Be("M2");
    }

    [Fact]
    public async Task GetMenuByIdQuery_WhenMenuExists_ShouldReturnProjection()
    {
        // Arrange
        _store.CourseCategories.Add(new CourseCategory { CourseId = 1, CourseName = "Dessert" });
        _store.Menus.Add(new Menu { MenuId = "M1", CourseId = 1, MenuName = "Leche Flan", Price = 120m, FoodCost = 40m });

        // Act
        var result = await _byId.ExecuteAsync("M1");

        // Assert
        result.Succeeded.Should().BeTrue();
        result.Data!.MenuId.Should().Be("M1");
        result.Data.CourseName.Should().Be("Dessert");

        // RSK-030: the projection no longer publishes a computed margin. A get-only computed
        // property cannot be projected by EF Core, so it always serialised as 0 — a field that
        // always reports zero is worse than no field.
        typeof(MenuResponse).GetProperty("ProfitMargin").Should().BeNull();
    }

    [Fact]
    public async Task GetMenuByIdQuery_WhenMenuDoesNotExist_ShouldReturnFailure()
    {
        // Act
        var result = await _byId.ExecuteAsync("NOPE");

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain("Menu item not found.");
    }

    [Fact]
    public async Task GetCoursesQuery_ShouldReturnCourseProjections()
    {
        // Arrange
        _store.CourseCategories.Add(new CourseCategory { CourseId = 2, CourseName = "Dessert" });
        _store.CourseCategories.Add(new CourseCategory { CourseId = 1, CourseName = "Main Course" });

        // Act
        var result = await _courses.ExecuteAsync();

        // Assert
        result.Succeeded.Should().BeTrue();
        result.Data.Should().HaveCount(2);
        result.Data![0].CourseId.Should().Be(1);
        result.Data[0].CourseName.Should().Be("Main Course");
    }
}
