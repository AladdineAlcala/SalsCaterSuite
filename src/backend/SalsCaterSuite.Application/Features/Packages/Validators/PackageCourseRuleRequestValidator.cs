using FluentValidation;
using SalsCaterSuite.Application.Common.Interfaces;
using SalsCaterSuite.Application.Features.Packages.DTOs;

namespace SalsCaterSuite.Application.Features.Packages.Validators;

/// <summary>
/// The validation rules of <b>one</b> submitted course rule (spec §3.6, BR-11, DEC-077).
/// <para>
/// The set validator composes this one, so the two can never drift apart.
/// </para>
/// <para>
/// The existence rules treat a <b>soft-deleted</b> course or add-on as "does not exist", so the
/// lookups filter <c>IsDeleted</c> (spec §3.6). The limits are the <b>column</b> limits:
/// <c>ruleDescription</c> is <c>nvarchar(500)</c> and <c>inclusionTag</c> is
/// <c>nvarchar(50)</c> — a wider limit would accept a payload the database then rejects as an
/// unhandled <c>500</c>, the defect BR-11 exists to close.
/// </para>
/// </summary>
public class PackageCourseRuleRequestValidator : AbstractValidator<PackageCourseRuleRequestDto>
{
    private readonly IPackageCourseRuleRepository _rules;

    public PackageCourseRuleRequestValidator(IPackageCourseRuleRepository rules)
    {
        _rules = rules;

        RuleFor(x => x.CourseId)
            .MustAsync(async (courseId, cancellationToken) =>
                await _rules.LiveCourseExistsAsync(courseId, cancellationToken))
            .WithMessage((rule, courseId) => $"Course category {courseId} does not exist.");

        RuleFor(x => x.RequiredQuota)
            .GreaterThanOrEqualTo(1)
            .WithMessage("Required quota must be at least 1.");

        RuleFor(x => x.PortionGrams)
            .GreaterThanOrEqualTo(0)
            .WithMessage("Portion weight must be zero or greater.");

        RuleFor(x => x.RuleDescription)
            .NotEmpty()
            .WithMessage("Rule description is required.")
            .MaximumLength(500)
            .WithMessage("Rule description must not exceed 500 characters.");

        RuleFor(x => x.InclusionTag)
            .MaximumLength(50)
            .WithMessage("Inclusion tag must not exceed 50 characters.");

        // A null upgrade means "no upgrade" — only a supplied id must resolve.
        RuleFor(x => x.UpgradeAddonId)
            .MustAsync(async (addonId, cancellationToken) =>
                !addonId.HasValue
                || await _rules.LiveAddonExistsAsync(addonId.Value, cancellationToken))
            .WithMessage((rule, addonId) => $"Add-on {addonId} does not exist.");
    }
}
