using FluentValidation;
using SalsCaterSuite.Application.Common.Interfaces;
using SalsCaterSuite.Application.Common.Models;
using SalsCaterSuite.Application.Features.Packages.DTOs.Request;
using SalsCaterSuite.Application.Features.Packages.DTOs.Response;
using SalsCaterSuite.Application.Features.Packages.Models;
using SalsCaterSuite.Application.Features.Packages.Queries;
using SalsCaterSuite.Domain.Entities;

namespace SalsCaterSuite.Application.Features.Packages.Commands;

/// <summary>
/// PUT /api/packages/{id}/course-rules — replace the tier's ruleset (contract §13.2, REQ-PKG-02).
/// <para>
/// A ruleset is one document, so the submitted set <b>replaces</b> the tier's ruleset: the
/// submitted entries are upserted on their natural key (the course), the entries that were not
/// submitted are <b>soft-deleted</b> — never destroyed (DEC-085) — and the whole change commits
/// in one <c>SaveChangesAsync</c> through the shared request-scoped context (DEC-039).
/// </para>
/// <para>
/// A re-added course <b>resurrects</b> its soft-deleted row instead of inserting a second one
/// (DEC-088): the natural key is matched over <b>all</b> rows, the soft-deleted ones included.
/// </para>
/// <para>
/// An <b>empty</b> set clears the tier's ruleset by soft-deleting every live rule — the
/// documented meaning of an empty set. A <c>null</c> set is a rejected payload.
/// </para>
/// </summary>
public class ReplacePackageCourseRulesCommand
{
    private readonly IPackageRepository _packages;
    private readonly IPackageCourseRuleRepository _rules;
    private readonly IValidator<PackageCourseRulesetRequest> _validator;
    private readonly GetPackageCourseRulesetQuery _ruleset;

    public ReplacePackageCourseRulesCommand(
        IPackageRepository packages,
        IPackageCourseRuleRepository rules,
        IValidator<PackageCourseRulesetRequest> validator,
        GetPackageCourseRulesetQuery ruleset)
    {
        _packages = packages;
        _rules = rules;
        _validator = validator;
        _ruleset = ruleset;
    }

    public async Task<Result<PackageCourseRulesetResponse>> ExecuteAsync(
        int packageId,
        PackageCourseRulesetRequest request,
        CancellationToken cancellationToken = default)
    {
        // The validation runs first, so a rejected payload never reaches the database (BR-11).
        var validationResult = await _validator.ValidateAsync(request, cancellationToken);

        if (!validationResult.IsValid)
        {
            return Result<PackageCourseRulesetResponse>.Failure(
                validationResult.Errors.Select(error => error.ErrorMessage).ToArray());
        }

        // The tier is resolved through the catalogue population: a soft-deleted tier is refused
        // by the write (DEC-074).
        var package = await _packages.GetProjectionByIdAsync(packageId, cancellationToken);

        if (package is null)
        {
            return new PackageNotFoundResult<PackageCourseRulesetResponse>("Package not found.");
        }

        // Every row the tier holds, the soft-deleted ones included, so a re-added course can
        // resurrect its row (DEC-088).
        var existing = await _rules.GetAllRulesAsync(packageId, cancellationToken);
        var byCourse = existing.ToLookup(rule => rule.CourseId);

        var submitted = request.Rules!.ToList();
        var submittedCourseIds = submitted.Select(rule => rule.CourseId).ToHashSet();

        foreach (var entry in submitted)
        {
            var match = byCourse[entry.CourseId].FirstOrDefault(rule => !rule.IsDeleted)
                        ?? byCourse[entry.CourseId].FirstOrDefault();

            if (match is null)
            {
                await _rules.AddAsync(new PackageCourseRule
                {
                    PackageId = packageId,
                    CourseId = entry.CourseId,
                    RequiredQuota = entry.RequiredQuota,
                    PortionGrams = entry.PortionGrams,
                    RuleDescription = entry.RuleDescription
                }, cancellationToken);

                continue;
            }

            match.RequiredQuota = entry.RequiredQuota;
            match.PortionGrams = entry.PortionGrams;
            match.RuleDescription = entry.RuleDescription;

            // The resurrect: a re-added natural key revives its soft-deleted row.
            match.IsDeleted = false;
        }

        // The rules that were not submitted are soft-deleted, never destroyed (DEC-085).
        foreach (var rule in existing.Where(rule => !rule.IsDeleted && !submittedCourseIds.Contains(rule.CourseId)))
        {
            rule.IsDeleted = true;
        }

        await _rules.SaveChangesAsync(cancellationToken);

        // The response is the new state, read back through the one ruleset projection.
        return await _ruleset.ExecuteAsync(packageId, cancellationToken);
    }
}
