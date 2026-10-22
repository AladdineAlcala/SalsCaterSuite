namespace SalsCaterSuite.UnitTests.Application.Validators;

using FluentAssertions;
using SalsCaterSuite.Application.Features.Packages.DTOs;
using SalsCaterSuite.Application.Features.Packages.Validators;
using SalsCaterSuite.Domain.Entities;
using SalsCaterSuite.UnitTests.Common.Fakes;
using Xunit;

/// <summary>
/// The course-ruleset validation rules (spec §3.6, BR-11, DEC-077).
/// <para>
/// The set validator owns the set-level rules (the member must be supplied, no duplicate course)
/// and composes the per-rule validator, so the two can never drift apart.
/// </para>
/// </summary>
public class PackageCourseRuleValidatorTests
{
    private readonly FakeSalsCaterSuiteDataStore _store = new();
    private readonly PackageCourseRulesetRequestValidator _setValidator;
    private readonly PackageCourseRuleRequestValidator _ruleValidator;

    public PackageCourseRuleValidatorTests()
    {
        _ruleValidator = new PackageCourseRuleRequestValidator(new FakePackageCourseRuleRepository(_store));
        _setValidator = new PackageCourseRulesetRequestValidator(_ruleValidator);

        _store.CourseCategories.Add(new CourseCategory { CourseId = 5, CourseName = "Main Course" });
        _store.CourseCategories.Add(new CourseCategory { CourseId = 6, CourseName = "Dessert", IsDeleted = true });
        _store.AddonDetails.Add(new AddonDetail { AddonId = 7, Description = "Premium seafood", UnitPrice = 250m, Unit = "per head" });
        _store.AddonDetails.Add(new AddonDetail { AddonId = 8, Description = "Retired upgrade", UnitPrice = 100m, IsDeleted = true });
    }

    private static PackageCourseRuleRequestDto ValidRule(int courseId = 5) =>
        new(courseId, 1, 120, "One beef selection", "Included", null);

    [Fact]
    public async Task A_valid_ruleset_should_pass()
    {
        var request = new PackageCourseRulesetRequest(new[] { ValidRule() });

        (await _setValidator.ValidateAsync(request)).IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task A_null_rules_member_should_be_rejected_and_never_treated_as_an_empty_set()
    {
        // An empty set clears the tier's ruleset (DEC-085); a null member is a rejected payload.
        var request = new PackageCourseRulesetRequest(null);

        var result = await _setValidator.ValidateAsync(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(error => error.ErrorMessage == "Rules must be supplied.");
    }

    [Fact]
    public async Task An_empty_ruleset_should_pass_because_it_clears_the_tier()
    {
        var request = new PackageCourseRulesetRequest(Array.Empty<PackageCourseRuleRequestDto>());

        (await _setValidator.ValidateAsync(request)).IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task A_duplicate_course_should_be_refused()
    {
        var request = new PackageCourseRulesetRequest(new[] { ValidRule(), ValidRule() });

        var result = await _setValidator.ValidateAsync(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(error => error.ErrorMessage == "Course category 5 is configured more than once.");
    }

    [Fact]
    public async Task An_unknown_course_should_be_refused()
    {
        var request = new PackageCourseRulesetRequest(new[] { ValidRule(99) });

        var result = await _setValidator.ValidateAsync(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(error => error.ErrorMessage == "Course category 99 does not exist.");
    }

    [Fact]
    public async Task A_soft_deleted_course_should_not_exist_for_the_workbench()
    {
        var request = new PackageCourseRulesetRequest(new[] { ValidRule(6) });

        var result = await _setValidator.ValidateAsync(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(error => error.ErrorMessage == "Course category 6 does not exist.");
    }

    [Fact]
    public async Task A_quota_below_one_should_be_refused()
    {
        var rule = ValidRule() with { RequiredQuota = 0 };

        var result = await _ruleValidator.ValidateAsync(rule);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(error => error.ErrorMessage == "Required quota must be at least 1.");
    }

    [Fact]
    public async Task A_negative_portion_should_be_refused()
    {
        var rule = ValidRule() with { PortionGrams = -1 };

        var result = await _ruleValidator.ValidateAsync(rule);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(error => error.ErrorMessage == "Portion weight must be zero or greater.");
    }

    [Fact]
    public async Task A_missing_description_should_be_refused()
    {
        var rule = ValidRule() with { RuleDescription = "   " };

        var result = await _ruleValidator.ValidateAsync(rule);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(error => error.ErrorMessage == "Rule description is required.");
    }

    [Fact]
    public async Task An_over_long_description_should_be_refused()
    {
        // The column is nvarchar(500) — the limit is the column limit (DEC-075).
        var rule = ValidRule() with { RuleDescription = new string('x', 501) };

        var result = await _ruleValidator.ValidateAsync(rule);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(error => error.ErrorMessage == "Rule description must not exceed 500 characters.");
    }

    [Fact]
    public async Task A_five_hundred_character_description_should_pass()
    {
        var rule = ValidRule() with { RuleDescription = new string('x', 500) };

        (await _ruleValidator.ValidateAsync(rule)).IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task An_over_long_inclusion_tag_should_be_refused()
    {
        var rule = ValidRule() with { InclusionTag = new string('x', 51) };

        var result = await _ruleValidator.ValidateAsync(rule);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(error => error.ErrorMessage == "Inclusion tag must not exceed 50 characters.");
    }

    [Fact]
    public async Task An_unknown_add_on_should_be_refused()
    {
        var rule = ValidRule() with { UpgradeAddonId = 99 };

        var result = await _ruleValidator.ValidateAsync(rule);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(error => error.ErrorMessage == "Add-on 99 does not exist.");
    }

    [Fact]
    public async Task A_soft_deleted_add_on_should_not_exist_for_the_workbench()
    {
        var rule = ValidRule() with { UpgradeAddonId = 8 };

        var result = await _ruleValidator.ValidateAsync(rule);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(error => error.ErrorMessage == "Add-on 8 does not exist.");
    }

    [Fact]
    public async Task A_live_add_on_should_pass()
    {
        var rule = ValidRule() with { UpgradeAddonId = 7 };

        (await _ruleValidator.ValidateAsync(rule)).IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task A_null_upgrade_should_pass_because_no_upgrade_is_a_valid_state()
    {
        var rule = ValidRule() with { UpgradeAddonId = null, PortionGrams = null, InclusionTag = null };

        (await _ruleValidator.ValidateAsync(rule)).IsValid.Should().BeTrue();
    }
}
