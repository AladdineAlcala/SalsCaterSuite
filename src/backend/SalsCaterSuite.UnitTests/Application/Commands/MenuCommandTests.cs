namespace SalsCaterSuite.UnitTests.Application.Commands;

using FluentAssertions;
using SalsCaterSuite.Application.Features.Menus;
using SalsCaterSuite.Application.Features.Menus.Commands;
using SalsCaterSuite.Application.Features.Menus.DTOs.Request;
using SalsCaterSuite.Application.Features.Menus.DTOs.Response;
using SalsCaterSuite.Application.Features.Menus.Models;
using SalsCaterSuite.Application.Features.Menus.Validators;
using SalsCaterSuite.Domain.Entities;
using SalsCaterSuite.UnitTests.Common.Fakes;
using Xunit;

/// <summary>
/// DEC-037: the menu write use cases are repository-backed (hand-written fake).
/// <para>
/// The create and the update command now run the menu validator (BR-11, spec §6.4) — the
/// constructor calls below therefore pass the validator as well.
/// </para>
/// </summary>
public class MenuCommandTests
{
    private readonly FakeSalsCaterSuiteDataStore _store = new();
    private readonly FakeMenuRepository _menus;
    private readonly CreateMenuCommand _create;
    private readonly UpdateMenuCommand _update;
    private readonly DeleteMenuCommand _delete;

    public MenuCommandTests()
    {
        _menus = new FakeMenuRepository(_store);

        // The validator is shared by the create and the update command; it needs the repository
        // because the course id must exist in CourseCategory (spec §6.4).
        var validator = new MenuRequestValidator(_menus);

        _create = new CreateMenuCommand(_menus, validator);
        _update = new UpdateMenuCommand(_menus, validator);
        _delete = new DeleteMenuCommand(_menus);

        _store.CourseCategories.Add(new CourseCategory { CourseId = 1, CourseName = "Main Course" });
    }

    private static CreateMenuRequest Request(string menuId) => new()
    {
        MenuId = menuId,
        CourseId = 1,
        MenuName = "Beef Stroganoff",
        Note = "Signature",
        Price = 350m,
        FoodCost = 105m
    };

    [Fact]
    public async Task Create_WhenMenuIdIsNew_ShouldPersistUppercasedMenuAndReturnId()
    {
        // Act
        var result = await _create.ExecuteAsync(Request("m001"));

        // Assert
        result.Succeeded.Should().BeTrue();
        result.Data.Should().Be("M001");
        _store.Menus.Should().ContainSingle();
        _store.Menus[0].MenuId.Should().Be("M001");
        _menus.SaveCount.Should().Be(1);
    }

    [Fact]
    public async Task Create_WhenMenuIdAlreadyExists_ShouldReturnConflictFailure()
    {
        // Arrange
        _store.Menus.Add(new Menu { MenuId = "M001", MenuName = "Existing" });

        // Act
        var result = await _create.ExecuteAsync(Request("M001"));

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Should().BeOfType<MenuDuplicateResult>();
        result.Errors.Should().Contain("Menu item 'M001' already exists.");
        _store.Menus.Should().ContainSingle();
        _menus.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task Create_WhenTheMenuIdIsAlreadyStoredInAnotherCase_ShouldReturnConflictFailure()
    {
        // Arrange — the dish code is upper-cased by the server, so the uniqueness check must see
        // the stored form.
        _store.Menus.Add(new Menu { MenuId = "M001", MenuName = "Existing" });

        // Act
        var result = await _create.ExecuteAsync(Request("m001"));

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Should().BeOfType<MenuDuplicateResult>();
        _store.Menus.Should().ContainSingle();
        _menus.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task Create_WhenTheNameIsBlank_ShouldReturnValidationFailureAndPersistNothing()
    {
        // Arrange
        var request = Request("M002");
        request.MenuName = "   ";

        // Act
        var result = await _create.ExecuteAsync(request);

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Should().NotBeOfType<MenuDuplicateResult>();
        result.Errors.Should().Contain("Menu name is required.");
        _store.Menus.Should().BeEmpty();
        _menus.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task Create_WhenThePriceIsNegative_ShouldReturnValidationFailure()
    {
        // Arrange
        var request = Request("M003");
        request.Price = -1m;

        // Act
        var result = await _create.ExecuteAsync(request);

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain("Price must be zero or greater.");
        _store.Menus.Should().BeEmpty();
        _menus.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task Create_WhenTheFoodCostIsNegative_ShouldReturnValidationFailure()
    {
        // Arrange
        var request = Request("M004");
        request.FoodCost = -0.01m;

        // Act
        var result = await _create.ExecuteAsync(request);

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain("Food cost must be zero or greater.");
        _store.Menus.Should().BeEmpty();
    }

    [Fact]
    public async Task Create_WhenTheNameIsTooLong_ShouldReturnValidationFailure()
    {
        // Arrange
        var request = Request("M005");
        request.MenuName = new string('x', 151);

        // Act
        var result = await _create.ExecuteAsync(request);

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain("Menu name must not exceed 150 characters.");
        _store.Menus.Should().BeEmpty();
    }

    [Fact]
    public async Task Create_WhenTheNoteIsTooLong_ShouldReturnValidationFailure()
    {
        // Arrange
        var request = Request("M006");
        request.Note = new string('n', 501);

        // Act
        var result = await _create.ExecuteAsync(request);

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain("Note must not exceed 500 characters.");
        _store.Menus.Should().BeEmpty();
    }

    [Fact]
    public async Task Create_WhenTheCourseIsUnknown_ShouldReturnValidationFailure()
    {
        // Arrange
        var request = Request("M007");
        request.CourseId = 99;

        // Act
        var result = await _create.ExecuteAsync(request);

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain("The selected course category does not exist.");
        _store.Menus.Should().BeEmpty();
        _menus.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task Update_WhenMenuExists_ShouldApplyChangesAndSave()
    {
        // Arrange
        _store.Menus.Add(new Menu { MenuId = "M001", MenuName = "Old Name", Price = 100m, FoodCost = 10m });

        // Act
        var result = await _update.ExecuteAsync("M001", Request("M001"));

        // Assert
        result.Succeeded.Should().BeTrue();
        var saved = _store.Menus.Single(m => m.MenuId == "M001");
        saved.MenuName.Should().Be("Beef Stroganoff");
        saved.Price.Should().Be(350m);
        saved.FoodCost.Should().Be(105m);
        _menus.SaveCount.Should().Be(1);
    }

    [Fact]
    public async Task Create_WithAPlatingPhoto_ShouldPersistTheImage()
    {
        // Arrange
        var request = Request("IMG-1");
        request.Image = "https://images.salscatersuite.local/dishes/ds-104.jpg";

        // Act
        var result = await _create.ExecuteAsync(request);

        // Assert
        result.Succeeded.Should().BeTrue();
        _store.Menus[0].Image.Should().Be("https://images.salscatersuite.local/dishes/ds-104.jpg");
    }

    [Fact]
    public async Task Update_WithAPlatingPhoto_ShouldApplyTheImage()
    {
        // Arrange
        _store.Menus.Add(new Menu { MenuId = "M001", MenuName = "Old Name", Price = 100m, FoodCost = 10m });
        var request = Request("M001");
        request.Image = "https://images.salscatersuite.local/dishes/m001.jpg";

        // Act
        var result = await _update.ExecuteAsync("M001", request);

        // Assert
        result.Succeeded.Should().BeTrue();
        _store.Menus.Single(m => m.MenuId == "M001").Image
            .Should().Be("https://images.salscatersuite.local/dishes/m001.jpg");
    }

    [Fact]
    public async Task Update_WhenMenuDoesNotExist_ShouldReturnNotFoundFailure()
    {
        // Act
        var result = await _update.ExecuteAsync("NOPE", Request("NOPE"));

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Should().BeOfType<MenuNotFoundResult>();
        result.Errors.Should().Contain("Menu item not found.");
        _menus.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task Update_WhenThePayloadIsInvalid_ShouldReturnValidationFailureAndChangeNothing()
    {
        // Arrange
        _store.Menus.Add(new Menu { MenuId = "M001", MenuName = "Old Name", Price = 100m, FoodCost = 10m });
        var request = Request("M001");
        request.MenuName = string.Empty;
        request.Price = -5m;

        // Act
        var result = await _update.ExecuteAsync("M001", request);

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Should().NotBeOfType<MenuNotFoundResult>();
        result.Errors.Should().Contain("Menu name is required.");
        result.Errors.Should().Contain("Price must be zero or greater.");
        _store.Menus.Single().MenuName.Should().Be("Old Name");
        _menus.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task Delete_WhenMenuExists_ShouldFlagTheDishAndKeepTheRow()
    {
        // Arrange
        _store.Menus.Add(new Menu { MenuId = "M001", MenuName = "To Delete" });

        // Act
        var result = await _delete.ExecuteAsync("M001");

        // Assert — DEC-063: the delete is a soft delete. The row stays in the kitchen's master
        // and is flagged; it is never physically destroyed.
        result.Succeeded.Should().BeTrue();
        _store.Menus.Should().ContainSingle();
        _store.Menus[0].IsDeleted.Should().BeTrue();
        _store.Menus[0].UpdatedDate.Should().NotBeNull();
        _menus.SaveCount.Should().Be(1);
    }

    [Fact]
    public async Task Delete_WhenMenuDoesNotExist_ShouldReturnNotFoundFailure()
    {
        // Act
        var result = await _delete.ExecuteAsync("NOPE");

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain("Menu item not found.");
        _menus.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task Delete_WhenTheDishIsAlreadyDeleted_ShouldReturnNotFoundFailure()
    {
        // Arrange — a soft-deleted dish is not found by the read, so it cannot be deleted twice.
        _store.Menus.Add(new Menu { MenuId = "M001", MenuName = "Already Deleted", IsDeleted = true });

        // Act
        var result = await _delete.ExecuteAsync("M001");

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain("Menu item not found.");
        _menus.SaveCount.Should().Be(0);
    }
}
