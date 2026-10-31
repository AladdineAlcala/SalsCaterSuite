namespace SalsCaterSuite.UnitTests.Application.Validators;

using FluentAssertions;
using SalsCaterSuite.Application.Features.Menus.DTOs.Request;
using SalsCaterSuite.Application.Features.Menus.DTOs.Response;
using SalsCaterSuite.Application.Features.Menus.Models;
using SalsCaterSuite.Application.Features.Menus.Validators;
using SalsCaterSuite.Domain.Entities;
using SalsCaterSuite.UnitTests.Common.Fakes;
using Xunit;

/// <summary>
/// The menu write validation rules (spec §6.4, BR-11, AC-M20).
/// <para>
/// Before this correction neither menu write command ran a validator, so a blank name, a
/// negative price, an over-length name or an unknown course id reached the database.
/// </para>
/// </summary>
public class MenuValidatorTests
{
    private readonly FakeSalsCaterSuiteDataStore _store = new();
    private readonly FakeMenuRepository _menus;
    private readonly MenuRequestValidator _validator;

    public MenuValidatorTests()
    {
        _menus = new FakeMenuRepository(_store);
        _validator = new MenuRequestValidator(_menus);

        _store.CourseCategories.Add(new CourseCategory { CourseId = 1, CourseName = "Main Course" });
    }

    private static CreateMenuRequest Valid() => new()
    {
        MenuId = "DS-104",
        CourseId = 1,
        MenuName = "Prime Roast Ribeye",
        Note = "Signature",
        Price = 420m,
        FoodCost = 138.60m
    };

    [Fact]
    public async Task A_valid_payload_should_pass()
    {
        var result = await _validator.ValidateAsync(Valid());

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task The_menu_id_should_be_required()
    {
        var request = Valid();
        request.MenuId = "   ";

        var result = await _validator.ValidateAsync(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage == "Menu id is required.");
    }

    [Fact]
    public async Task The_menu_id_should_not_exceed_the_column_length()
    {
        var request = Valid();
        request.MenuId = new string('x', 51);

        var result = await _validator.ValidateAsync(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage == "Menu id must not exceed 50 characters.");
    }

    [Fact]
    public async Task A_fifty_character_menu_id_should_pass()
    {
        var request = Valid();
        request.MenuId = new string('x', 50);

        (await _validator.ValidateAsync(request)).IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task The_course_should_be_required()
    {
        var request = Valid();
        request.CourseId = 0;

        var result = await _validator.ValidateAsync(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage == "A course category is required.");
    }

    [Fact]
    public async Task The_course_should_exist()
    {
        var request = Valid();
        request.CourseId = 99;

        var result = await _validator.ValidateAsync(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage == "The selected course category does not exist.");
    }

    [Fact]
    public async Task The_name_should_be_required()
    {
        var request = Valid();
        request.MenuName = " ";

        var result = await _validator.ValidateAsync(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage == "Menu name is required.");
    }

    [Fact]
    public async Task The_name_should_not_exceed_the_column_length()
    {
        var request = Valid();
        request.MenuName = new string('x', 151);

        var result = await _validator.ValidateAsync(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage == "Menu name must not exceed 150 characters.");
    }

    [Fact]
    public async Task A_one_hundred_and_fifty_character_name_should_pass()
    {
        var request = Valid();
        request.MenuName = new string('x', 150);

        (await _validator.ValidateAsync(request)).IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task The_note_should_be_optional()
    {
        var request = Valid();
        request.Note = null;

        (await _validator.ValidateAsync(request)).IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task The_note_should_not_exceed_the_column_length()
    {
        var request = Valid();
        request.Note = new string('n', 501);

        var result = await _validator.ValidateAsync(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage == "Note must not exceed 500 characters.");
    }

    [Fact]
    public async Task The_image_should_be_optional()
    {
        var request = Valid();
        request.Image = null;

        (await _validator.ValidateAsync(request)).IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task The_image_should_not_exceed_the_column_length()
    {
        var request = Valid();
        request.Image = new string('h', 251);

        var result = await _validator.ValidateAsync(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage == "Image must not exceed 250 characters.");
    }

    [Fact]
    public async Task A_two_hundred_and_fifty_character_image_should_pass()
    {
        var request = Valid();
        request.Image = new string('h', 250);

        (await _validator.ValidateAsync(request)).IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task The_price_should_not_be_negative()    {
        var request = Valid();
        request.Price = -0.01m;

        var result = await _validator.ValidateAsync(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage == "Price must be zero or greater.");
    }

    [Fact]
    public async Task The_food_cost_should_not_be_negative()
    {
        var request = Valid();
        request.FoodCost = -1m;

        var result = await _validator.ValidateAsync(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage == "Food cost must be zero or greater.");
    }

    [Fact]
    public async Task A_zero_price_and_a_zero_food_cost_should_pass()
    {
        var request = Valid();
        request.Price = 0m;
        request.FoodCost = 0m;

        (await _validator.ValidateAsync(request)).IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Every_broken_rule_should_be_reported_at_once()
    {
        var request = new CreateMenuRequest
        {
            MenuId = string.Empty,
            CourseId = 0,
            MenuName = string.Empty,
            Note = new string('n', 501),
            Price = -1m,
            FoodCost = -1m
        };

        var result = await _validator.ValidateAsync(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage == "Menu id is required.");
        result.Errors.Should().Contain(e => e.ErrorMessage == "A course category is required.");
        result.Errors.Should().Contain(e => e.ErrorMessage == "The selected course category does not exist.");
        result.Errors.Should().Contain(e => e.ErrorMessage == "Menu name is required.");
        result.Errors.Should().Contain(e => e.ErrorMessage == "Note must not exceed 500 characters.");
        result.Errors.Should().Contain(e => e.ErrorMessage == "Price must be zero or greater.");
        result.Errors.Should().Contain(e => e.ErrorMessage == "Food cost must be zero or greater.");
    }

    // ─────────────────────────── the preview validator ───────────────────────────

    [Fact]
    public async Task The_preview_should_reject_a_negative_price()
    {
        var result = await new MenuCostingPreviewValidator().ValidateAsync(
            new MenuCostingPreviewRequest { SellingPrice = -1m, FoodCost = 10m });

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage == "Selling price must be zero or greater.");
    }

    [Fact]
    public async Task The_preview_should_reject_a_negative_food_cost()
    {
        var result = await new MenuCostingPreviewValidator().ValidateAsync(
            new MenuCostingPreviewRequest { SellingPrice = 100m, FoodCost = -0.01m });

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage == "Food cost must be zero or greater.");
    }

    [Fact]
    public async Task The_preview_should_accept_a_zero_price()
    {
        var result = await new MenuCostingPreviewValidator().ValidateAsync(
            new MenuCostingPreviewRequest { SellingPrice = 0m, FoodCost = 0m });

        result.IsValid.Should().BeTrue();
    }
}
