namespace SalsCaterSuite.UnitTests.Application.Validators;

using FluentAssertions;
using SalsCaterSuite.Application.Features.Packages.DTOs.Request;
using SalsCaterSuite.Application.Features.Packages.DTOs.Response;
using SalsCaterSuite.Application.Features.Packages.Models;
using SalsCaterSuite.Application.Features.Packages.Validators;
using Xunit;

/// <summary>
/// The package write validation rules (spec §3.3, BR-11, DEC-069).
/// <para>
/// Before this correction neither package write command ran a validator, so a blank tier type,
/// a blank description, a negative rate and a minimum headcount below one reached the database.
/// </para>
/// </summary>
public class PackageRequestValidatorTests
{
    private readonly PackageRequestValidator _validator = new();

    private static CreatePackageRequest Valid() =>
        new("Buffet Premium", "Premium buffet package", 650m, 100, 4);

    [Fact]
    public async Task A_valid_payload_should_pass()
    {
        var result = await _validator.ValidateAsync(Valid());

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task The_package_type_should_be_required()
    {
        var request = Valid() with { PackageType = "   " };

        var result = await _validator.ValidateAsync(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage == "Package type is required.");
    }

    [Fact]
    public async Task The_package_type_should_not_exceed_the_allowed_length()
    {
        // The column is nvarchar(50), so 51 characters must be refused here rather than
        // reaching the database and failing as an unhandled 500 (DEC-075).
        var request = Valid() with { PackageType = new string('x', 51) };

        var result = await _validator.ValidateAsync(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage == "Package type must not exceed 50 characters.");
    }

    [Fact]
    public async Task A_fifty_character_package_type_should_pass()
    {
        var request = Valid() with { PackageType = new string('x', 50) };

        (await _validator.ValidateAsync(request)).IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task The_description_should_be_required()
    {
        var request = Valid() with { Description = string.Empty };

        var result = await _validator.ValidateAsync(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage == "Package description is required.");
    }

    [Fact]
    public async Task The_description_should_not_exceed_the_allowed_length()
    {
        var request = Valid() with { Description = new string('d', 251) };

        var result = await _validator.ValidateAsync(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage == "Package description must not exceed 250 characters.");
    }

    [Fact]
    public async Task A_two_hundred_and_fifty_character_description_should_pass()
    {
        var request = Valid() with { Description = new string('d', 250) };

        (await _validator.ValidateAsync(request)).IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task The_rate_should_not_be_negative()
    {
        var request = Valid() with { AmountPax = -0.01m };

        var result = await _validator.ValidateAsync(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage == "Rate per guest must be zero or greater.");
    }

    [Fact]
    public async Task A_zero_rate_should_pass()
    {
        var request = Valid() with { AmountPax = 0m };

        (await _validator.ValidateAsync(request)).IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task An_absent_rate_should_pass()
    {
        // A null rate means "not priced" — it is not an error (spec §3.3).
        var request = Valid() with { AmountPax = null };

        (await _validator.ValidateAsync(request)).IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task The_minimum_pax_should_be_at_least_one()
    {
        var request = Valid() with { MinPax = 0 };

        var result = await _validator.ValidateAsync(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage == "Minimum pax must be at least 1.");
    }

    [Fact]
    public async Task A_negative_minimum_pax_should_be_rejected()
    {
        var request = Valid() with { MinPax = -5 };

        var result = await _validator.ValidateAsync(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage == "Minimum pax must be at least 1.");
    }

    [Fact]
    public async Task An_absent_minimum_pax_should_pass()
    {
        // A null floor means "not set" — it is not an error (spec §3.3).
        var request = Valid() with { MinPax = null };

        (await _validator.ValidateAsync(request)).IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task An_unknown_tier_type_should_pass()
    {
        // DEC-070: the column is free text and the type is not restricted to the PackageType
        // enum members — the legacy import may carry other values.
        var request = Valid() with { PackageType = "Wedding Grand" };

        (await _validator.ValidateAsync(request)).IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("Regular")]
    [InlineData("Premier")]
    [InlineData("regular")]
    [InlineData("  Premier  ")]
    public async Task A_regular_or_premier_tier_may_carry_the_extended_location_amount(string packageType)
    {
        var request = Valid() with { PackageType = packageType, ExtendedLocationAmount = 125m };

        (await _validator.ValidateAsync(request)).IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("Wedding")]
    [InlineData("Vip")]
    [InlineData("Pm")]
    [InlineData("Sd")]
    [InlineData("Premier Buffet")]
    public async Task Any_other_type_may_not_carry_the_extended_location_amount(string packageType)
    {
        var request = Valid() with { PackageType = packageType, ExtendedLocationAmount = 125m };

        var result = await _validator.ValidateAsync(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e =>
            e.ErrorMessage == "The extended location amount applies only to the Regular and Premier tier types.");
    }

    [Fact]
    public async Task An_absent_extended_location_amount_passes_for_any_type()
    {
        var request = Valid() with { PackageType = "Wedding", ExtendedLocationAmount = null };

        (await _validator.ValidateAsync(request)).IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task The_extended_location_amount_should_not_be_negative()
    {
        var request = Valid() with { PackageType = "Regular", ExtendedLocationAmount = -0.01m };

        var result = await _validator.ValidateAsync(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage == "Extended location amount must be zero or greater.");
    }

    [Fact]
    public async Task Every_broken_rule_should_be_reported_at_once()
    {
        var request = new CreatePackageRequest(string.Empty, string.Empty, -1m, 0, 0);

        var result = await _validator.ValidateAsync(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage == "Package type is required.");
        result.Errors.Should().Contain(e => e.ErrorMessage == "Package description is required.");
        result.Errors.Should().Contain(e => e.ErrorMessage == "Rate per guest must be zero or greater.");
        result.Errors.Should().Contain(e => e.ErrorMessage == "Minimum pax must be at least 1.");
        result.Errors.Should().Contain(e => e.ErrorMessage == "Applicable pax classification is required.");
    }

    // ─────────────────── the applicable-pax classification (REQ-PKG-04, DEC-173) ───────────────────

    [Fact]
    public async Task The_applicable_pax_classification_should_be_required()
    {
        // The identifier is omitted, so it binds the C# default of zero.
        var request = Valid() with { NumPaxId = 0 };

        var result = await _validator.ValidateAsync(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage == "Applicable pax classification is required.");
    }

    [Fact]
    public async Task A_negative_applicable_pax_classification_should_be_rejected()
    {
        var request = Valid() with { NumPaxId = -1 };

        var result = await _validator.ValidateAsync(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage == "Applicable pax classification is required.");
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public async Task Any_of_the_five_known_classifications_should_pass(int numPaxId)
    {
        // The validator is SYNCHRONOUS and PARAMETERLESS (DEC-173): it checks the SHAPE of the
        // identifier, never whether a lookup row exists. The referential half lives in the
        // commands, which answer "Applicable pax classification was not found.".
        var request = Valid() with { NumPaxId = numPaxId };

        (await _validator.ValidateAsync(request)).IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task An_unknown_classification_should_pass_the_shape_check()
    {
        // The identifier is well-formed but references no lookup row — the referential half
        // rejects it in the command, not here.
        var request = Valid() with { NumPaxId = 999 };

        (await _validator.ValidateAsync(request)).IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task The_validator_should_not_tie_the_classification_to_the_package_type()
    {
        // AC-P19 / DEC-173: the classification is informational. It is never coupled to the
        // tier type — option 5 is not tied to PackageType.Pm, and no type forbids any option.
        foreach (var packageType in new[] { "Regular", "Premier", "Wedding", "Vip", "Pm", "Sd" })
        {
            var request = Valid() with { PackageType = packageType, NumPaxId = 5 };

            (await _validator.ValidateAsync(request)).IsValid.Should().BeTrue(
                $"the classification must not constrain the tier type '{packageType}'");
        }
    }

    [Fact]
    public async Task The_validator_should_not_tie_the_classification_to_the_minimum_pax()
    {
        // AC-P19: the classification never becomes a headcount rule.
        var request = Valid() with { MinPax = 1, NumPaxId = 1 };

        (await _validator.ValidateAsync(request)).IsValid.Should().BeTrue();
    }
}
