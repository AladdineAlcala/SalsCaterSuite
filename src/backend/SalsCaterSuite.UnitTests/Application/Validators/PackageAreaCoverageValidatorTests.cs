namespace SalsCaterSuite.UnitTests.Application.Validators;

using FluentAssertions;
using SalsCaterSuite.Application.Features.Packages.DTOs;
using SalsCaterSuite.Application.Features.Packages.Validators;
using SalsCaterSuite.Domain.Entities;
using SalsCaterSuite.UnitTests.Common.Fakes;
using Xunit;

/// <summary>
/// The coverage-set validation rules (spec §3.6, BR-11, DEC-077).
/// <para>
/// The set validator owns the set-level rules (the member must be supplied, no duplicate
/// municipality) and composes the per-entry validator, so the two can never drift apart.
/// </para>
/// </summary>
public class PackageAreaCoverageValidatorTests
{
    private readonly FakeSalsCaterSuiteDataStore _store = new();
    private readonly PackageAreaCoverageRequestValidator _setValidator;
    private readonly PackageAreaCoverageEntryValidator _entryValidator;

    public PackageAreaCoverageValidatorTests()
    {
        _entryValidator = new PackageAreaCoverageEntryValidator(new FakeAreaRepository(_store));
        _setValidator = new PackageAreaCoverageRequestValidator(_entryValidator);

        _store.Areas.Add(new Area { AreaId = 3, AreaDetails = "Clark" });
        _store.Areas.Add(new Area { AreaId = 4, AreaDetails = "Retired municipality", IsDeleted = true });
    }

    private static PackageAreaCoverageEntryDto ValidEntry(int areaId = 3) => new(areaId, true);

    [Fact]
    public async Task A_valid_coverage_set_should_pass()
    {
        var request = new PackageAreaCoverageRequest(new[] { ValidEntry() });

        (await _setValidator.ValidateAsync(request)).IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task A_null_coverages_member_should_be_rejected_and_never_treated_as_an_empty_set()
    {
        // An empty set clears the tier's coverage (DEC-085); a null member is a rejected payload.
        var request = new PackageAreaCoverageRequest(null);

        var result = await _setValidator.ValidateAsync(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(error => error.ErrorMessage == "Coverages must be supplied.");
    }

    [Fact]
    public async Task An_empty_coverage_set_should_pass_because_it_clears_the_tier()
    {
        var request = new PackageAreaCoverageRequest(Array.Empty<PackageAreaCoverageEntryDto>());

        (await _setValidator.ValidateAsync(request)).IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task A_duplicate_municipality_should_be_refused()
    {
        var request = new PackageAreaCoverageRequest(new[] { ValidEntry(), ValidEntry() });

        var result = await _setValidator.ValidateAsync(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(error => error.ErrorMessage == "Area 3 is covered more than once.");
    }

    [Fact]
    public async Task An_unknown_municipality_should_be_refused()
    {
        var request = new PackageAreaCoverageRequest(new[] { ValidEntry(99) });

        var result = await _setValidator.ValidateAsync(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(error => error.ErrorMessage == "Area 99 does not exist.");
    }

    [Fact]
    public async Task A_soft_deleted_municipality_should_not_exist_for_the_workbench()
    {
        var request = new PackageAreaCoverageRequest(new[] { ValidEntry(4) });

        var result = await _setValidator.ValidateAsync(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(error => error.ErrorMessage == "Area 4 does not exist.");
    }

    [Fact]
    public async Task An_inactive_coverage_should_pass_because_it_is_a_valid_state()
    {
        var entry = ValidEntry() with { IsActive = false };

        (await _entryValidator.ValidateAsync(entry)).IsValid.Should().BeTrue();
    }
}
