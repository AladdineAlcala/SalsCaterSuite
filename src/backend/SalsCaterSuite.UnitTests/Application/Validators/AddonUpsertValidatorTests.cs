namespace SalsCaterSuite.UnitTests.Application.Validators;

using FluentAssertions;
using SalsCaterSuite.Application.Features.Addons.DTOs.Request;
using SalsCaterSuite.Application.Features.Addons.DTOs.Response;
using SalsCaterSuite.Application.Features.Addons.Validators;
using SalsCaterSuite.Domain.Enums;
using Xunit;

/// <summary>
/// The add-on write validation (the owner's 2026-10-24 Settings → Add-ons request). The limits
/// are the columns: <c>addonDescription</c> is <c>nvarchar(150)</c> and <c>unit</c> is
/// <c>nvarchar(50)</c>, so a longer payload is a 400 here rather than a 500 at the database.
/// </summary>
public class AddonUpsertValidatorTests
{
    private readonly AddonUpsertValidator _validator = new();

    private static AddonUpsertRequest Valid() => new()
    {
        Description = "Carving station",
        UnitPrice = 1500m,
        Unit = "event",
        PricingBasis = AddonPricingBasis.Flat,
        IsActive = true
    };

    [Fact]
    public async Task A_valid_payload_should_pass()
    {
        (await _validator.ValidateAsync(Valid())).IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task The_description_should_be_required()
    {
        var result = await _validator.ValidateAsync(Valid() with { Description = "   " });

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage == "Add-on description is required.");
    }

    [Fact]
    public async Task The_description_should_not_exceed_the_column_length()
    {
        var result = await _validator.ValidateAsync(Valid() with { Description = new string('x', 151) });

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e =>
            e.ErrorMessage == "Add-on description must not exceed 150 characters.");
    }

    [Fact]
    public async Task The_unit_should_not_exceed_the_column_length()
    {
        var result = await _validator.ValidateAsync(Valid() with { Unit = new string('x', 51) });

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage == "Unit must not exceed 50 characters.");
    }

    [Fact]
    public async Task The_unit_price_should_not_be_negative()
    {
        var result = await _validator.ValidateAsync(Valid() with { UnitPrice = -0.01m });

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage == "Unit price must be zero or greater.");
    }

    [Fact]
    public async Task A_zero_unit_price_should_pass()
    {
        (await _validator.ValidateAsync(Valid() with { UnitPrice = 0m })).IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task An_unstated_pricing_basis_should_pass()
    {
        (await _validator.ValidateAsync(Valid() with { PricingBasis = null })).IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task An_undefined_pricing_basis_should_be_rejected()
    {
        var result = await _validator.ValidateAsync(
            Valid() with { PricingBasis = (AddonPricingBasis)99 });

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage == "The pricing basis is not valid.");
    }

    [Fact]
    public async Task A_non_positive_category_should_be_rejected()
    {
        var result = await _validator.ValidateAsync(Valid() with { AddonCategoryId = 0 });

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e =>
            e.ErrorMessage == "The add-on category must be a positive identifier.");
    }

    [Fact]
    public async Task A_non_positive_department_should_be_rejected()
    {
        var result = await _validator.ValidateAsync(Valid() with { DeptId = 0 });

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e =>
            e.ErrorMessage == "The department must be a positive identifier.");
    }

    [Fact]
    public async Task An_absent_category_and_department_should_pass()
    {
        var request = Valid() with { AddonCategoryId = null, DeptId = null };

        (await _validator.ValidateAsync(request)).IsValid.Should().BeTrue();
    }
}
