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
/// The existence rules treat a <b>soft-deleted</b> course as "does not exist", so the lookup
/// filters <c>IsDeleted</c> (spec §3.6). The limit is the <b>column</b> limit:
/// <c>ruleDescription</c> is <c>nvarchar(500)</c> — a wider limit would accept a payload the
/// database then rejects as an unhandled <c>500</c>, the defect BR-11 exists to close.
/// </para>
/// <para>
/// The retired inclusion-tag and upgrade-add-on rules were removed with the fields themselves
/// (DEC-172); the rule no longer references the add-on master.
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
    }
}
