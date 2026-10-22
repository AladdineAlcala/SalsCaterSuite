using FluentValidation;
using SalsCaterSuite.Application.Features.Packages.DTOs;

namespace SalsCaterSuite.Application.Features.Packages.Validators;

/// <summary>
/// The validation rules of the <b>ruleset</b> — the whole set a tier holds (spec §3.6, BR-11,
/// DEC-085).
/// <para>
/// A ruleset is one document, so the set-level rules live here and the per-rule rules are
/// composed from <see cref="PackageCourseRuleRequestValidator"/>.
/// </para>
/// <para>
/// The <c>rules</c> member is <b>nullable</b> on purpose: an absent or explicitly null member is
/// a <b>rejected payload</b> (<c>400</c>), never an empty set — an empty set has the
/// documented, destructive meaning of clearing the tier's ruleset (DEC-085).
/// </para>
/// </summary>
public class PackageCourseRulesetRequestValidator : AbstractValidator<PackageCourseRulesetRequest>
{
    private readonly IValidator<PackageCourseRuleRequestDto> _ruleValidator;

    public PackageCourseRulesetRequestValidator(IValidator<PackageCourseRuleRequestDto> ruleValidator)
    {
        _ruleValidator = ruleValidator;

        RuleFor(x => x.Rules)
            .NotNull()
            .WithMessage("Rules must be supplied.");

        // The per-rule rules, applied to every member of the submitted set.
        RuleForEach(x => x.Rules!)
            .SetValidator(_ruleValidator);

        // A duplicate natural key inside one submitted set is a 400, not a last-one-wins
        // (DEC-085). The database's filtered unique index would refuse the second live row
        // anyway; the validator refuses it before anything is persisted.
        RuleFor(x => x.Rules)
            .Must(rules => rules is null
                || rules.Select(rule => rule.CourseId).Distinct().Count() == rules.Count)
            .WithMessage(request => $"Course category {FirstDuplicateCourseId(request)} is configured more than once.");
    }

    /// <summary>The first course id the submitted set configures more than once.</summary>
    private static int FirstDuplicateCourseId(PackageCourseRulesetRequest request) =>
        request.Rules?
            .GroupBy(rule => rule.CourseId)
            .FirstOrDefault(group => group.Count() > 1)?
            .Key ?? 0;
}
