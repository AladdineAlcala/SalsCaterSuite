namespace SalsCaterSuite.UnitTests.Application.Validators;

using FluentAssertions;
using SalsCaterSuite.Application.Features.Packages.DTOs;
using SalsCaterSuite.Application.Features.Packages.Validators;
using Xunit;

/// <summary>
/// The municipality validation rules (spec §3.6, BR-11, DEC-077, DEC-081, DEC-086).
/// </summary>
public class AreaRequestValidatorTests
{
    private readonly AreaRequestValidator _validator = new();

    private static AreaRequestDto Valid() =>
        new("Clark", 150m, 42, "Reefer Truck & Mobile Gen", 3200m);

    [Fact]
    public async Task A_valid_payload_should_pass()
    {
        (await _validator.ValidateAsync(Valid())).IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task A_municipality_without_a_stated_rate_should_pass()
    {
        // A null rate is "no rate has been stated" — not an error (DEC-086).
        var request = new AreaRequestDto("Municipality", null, null, null, null);

        (await _validator.ValidateAsync(request)).IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task The_name_should_be_required()
    {
        var request = Valid() with { AreaDetails = "   " };

        var result = await _validator.ValidateAsync(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(error => error.ErrorMessage == "Area name is required.");
    }

    [Fact]
    public async Task The_name_should_not_exceed_the_column_limit()
    {
        // The column is nvarchar(150).
        var request = Valid() with { AreaDetails = new string('x', 151) };

        var result = await _validator.ValidateAsync(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(error => error.ErrorMessage == "Area name must not exceed 150 characters.");
    }

    [Fact]
    public async Task A_fifty_character_name_should_pass()
    {
        var request = Valid() with { AreaDetails = new string('x', 150) };

        (await _validator.ValidateAsync(request)).IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task A_negative_rate_should_be_refused()
    {
        var request = Valid() with { AdditionalRate = -1m };

        var result = await _validator.ValidateAsync(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(error => error.ErrorMessage == "Additional rate must be zero or greater.");
    }

    [Fact]
    public async Task A_negative_distance_should_be_refused()
    {
        var request = Valid() with { DistanceKm = -1 };

        var result = await _validator.ValidateAsync(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(error => error.ErrorMessage == "Distance must be zero or greater.");
    }

    [Fact]
    public async Task An_over_long_dispatch_note_should_be_refused()
    {
        var request = Valid() with { LogisticsNote = new string('x', 151) };

        var result = await _validator.ValidateAsync(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(error => error.ErrorMessage == "Logistics note must not exceed 150 characters.");
    }

    [Fact]
    public async Task A_negative_logistics_cost_should_be_refused()
    {
        var request = Valid() with { LogisticsCost = -0.01m };

        var result = await _validator.ValidateAsync(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(error => error.ErrorMessage == "Logistics cost must be zero or greater.");
    }

    [Fact]
    public async Task A_zero_logistics_cost_should_pass_because_zero_is_a_real_measurement()
    {
        var request = Valid() with { LogisticsCost = 0m, AdditionalRate = 0m, DistanceKm = 0 };

        (await _validator.ValidateAsync(request)).IsValid.Should().BeTrue();
    }
}
