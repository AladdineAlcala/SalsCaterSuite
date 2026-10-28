namespace SalsCaterSuite.UnitTests.Application.Commands;

using FluentAssertions;
using SalsCaterSuite.Application.Features.Packages;
using SalsCaterSuite.Application.Features.Packages.Commands;
using SalsCaterSuite.Application.Features.Packages.DTOs;
using SalsCaterSuite.Application.Features.Packages.Queries;
using SalsCaterSuite.Application.Features.Packages.Validators;
using SalsCaterSuite.Domain.Entities;
using SalsCaterSuite.UnitTests.Common.Fakes;
using Xunit;

/// <summary>
/// The ruleset replacement (contract §13.2, DEC-085, DEC-088, BR-11).
/// <para>
/// A ruleset is one document: the submitted set replaces the tier's ruleset, a rule that was not
/// submitted is <b>soft-deleted</b> — never destroyed — and a re-added course <b>resurrects</b> its
/// soft-deleted row instead of inserting a second one.
/// </para>
/// </summary>
public class PackageCourseRuleCommandTests
{
    private readonly FakeSalsCaterSuiteDataStore _store = new();
    private readonly FakePackageCourseRuleRepository _rules;
    private readonly ReplacePackageCourseRulesCommand _command;

    public PackageCourseRuleCommandTests()
    {
        _store.Packages.Add(new Package
        {
            PackageId = 1,
            PackageType = "Wedding",
            Description = "Wedding Grand Tier",
            AmountPax = 1450m,
            MinPax = 150,
            IsActive = true,
            NumPaxId = 4,
        });

        _store.CourseCategories.Add(new CourseCategory { CourseId = 1, CourseName = "Main Course" });
        _store.CourseCategories.Add(new CourseCategory { CourseId = 2, CourseName = "Dessert" });

        _rules = new FakePackageCourseRuleRepository(_store);

        _command = new ReplacePackageCourseRulesCommand(
            new FakePackageRepository(_store),
            _rules,
            new PackageCourseRulesetRequestValidator(new PackageCourseRuleRequestValidator(_rules)),
            new GetPackageCourseRulesetQuery(
                new FakePackageRepository(_store),
                _rules,
                new FakeMenuRepository(_store),
                new FakeFoodCostBenchmarkProvider(35m),
                new FakeDateTime(new DateTime(2026, 10, 22, 9, 0, 0, DateTimeKind.Utc))));
    }

    private static PackageCourseRulesetRequest Request(params PackageCourseRuleRequestDto[] rules) =>
        new(rules);

    private static PackageCourseRuleRequestDto Rule(int courseId) =>
        new(courseId, 1, 120, "One selection");

    [Fact]
    public async Task A_rejected_payload_should_persist_nothing()
    {
        var result = await _command.ExecuteAsync(1, Request(new PackageCourseRuleRequestDto(99, 1, null, "Unknown course")));

        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain("Course category 99 does not exist.");
        _store.PackageCourseRules.Should().BeEmpty();
        _rules.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task A_rule_removed_from_the_submitted_set_should_be_soft_deleted_and_not_destroyed()
    {
        _store.PackageCourseRules.Add(new PackageCourseRule
        {
            PackageCourseRuleId = 1,
            PackageId = 1,
            CourseId = 1,
            RequiredQuota = 1,
            RuleDescription = "One main course"
        });

        var result = await _command.ExecuteAsync(1, Request(Rule(2)));

        result.Succeeded.Should().BeTrue();

        // The row survives for audit — it is flagged, not destroyed (DEC-085).
        _store.PackageCourseRules.Should().HaveCount(2);
        _store.PackageCourseRules.Single(rule => rule.CourseId == 1).IsDeleted.Should().BeTrue();
        _store.PackageCourseRules.Single(rule => rule.CourseId == 2).IsDeleted.Should().BeFalse();

        // The published state no longer carries it.
        result.Data!.Rules.Should().ContainSingle();
        result.Data.Rules[0].CourseId.Should().Be(2);
    }

    [Fact]
    public async Task An_empty_set_should_clear_the_tier_by_soft_deleting_every_live_rule()
    {
        _store.PackageCourseRules.Add(new PackageCourseRule
        {
            PackageCourseRuleId = 1,
            PackageId = 1,
            CourseId = 1,
            RequiredQuota = 1,
            RuleDescription = "One main course"
        });

        var result = await _command.ExecuteAsync(1, Request());

        result.Succeeded.Should().BeTrue();
        _store.PackageCourseRules.Should().ContainSingle();
        _store.PackageCourseRules[0].IsDeleted.Should().BeTrue();
        result.Data!.Rules.Should().BeEmpty();
    }

    [Fact]
    public async Task A_re_added_course_should_resurrect_its_soft_deleted_row()
    {
        _store.PackageCourseRules.Add(new PackageCourseRule
        {
            PackageCourseRuleId = 1,
            PackageId = 1,
            CourseId = 1,
            RequiredQuota = 1,
            RuleDescription = "One main course"
        });

        // Remove it…
        await _command.ExecuteAsync(1, Request());
        _store.PackageCourseRules[0].IsDeleted.Should().BeTrue();

        // …and add it back.
        var result = await _command.ExecuteAsync(1, Request(Rule(1)));

        result.Succeeded.Should().BeTrue();

        // The very same row came back to life — no second row was created (DEC-088).
        _store.PackageCourseRules.Should().ContainSingle();
        _store.PackageCourseRules[0].PackageCourseRuleId.Should().Be(1);
        _store.PackageCourseRules[0].IsDeleted.Should().BeFalse();
        _store.PackageCourseRules[0].RequiredQuota.Should().Be(1);
        result.Data!.Rules.Should().ContainSingle();
    }

    [Fact]
    public async Task A_submitted_rule_should_update_the_existing_row_in_place()
    {
        _store.PackageCourseRules.Add(new PackageCourseRule
        {
            PackageCourseRuleId = 1,
            PackageId = 1,
            CourseId = 1,
            RequiredQuota = 1,
            RuleDescription = "One main course"
        });

        var result = await _command.ExecuteAsync(1, Request(Rule(1) with { RequiredQuota = 3, RuleDescription = "Three main courses" }));

        result.Succeeded.Should().BeTrue();
        _store.PackageCourseRules.Should().ContainSingle();
        _store.PackageCourseRules[0].RequiredQuota.Should().Be(3);
        _store.PackageCourseRules[0].RuleDescription.Should().Be("Three main courses");
    }

    [Fact]
    public async Task A_soft_deleted_tier_should_be_refused_and_no_write_should_touch_it()
    {
        _store.Packages[0].IsDeleted = true;

        var result = await _command.ExecuteAsync(1, Request(Rule(1)));

        result.Succeeded.Should().BeFalse();
        result.Should().BeOfType<PackageNotFoundResult<PackageCourseRulesetDto>>();
        result.Errors.Should().ContainSingle().Which.Should().Be("Package not found.");
        _store.PackageCourseRules.Should().BeEmpty();
        _rules.SaveCount.Should().Be(0);
    }
}
