using FluentValidation;
using SalsCaterSuite.Application.Features.Packages.DTOs.Request;
using SalsCaterSuite.Application.Features.Packages.DTOs.Response;
using SalsCaterSuite.Application.Features.Packages.Models;

namespace SalsCaterSuite.Application.Features.Packages.Validators;

/// <summary>
/// The validation rules of the <b>coverage set</b> — the whole set a tier holds (spec §3.6,
/// BR-11, DEC-085).
/// <para>
/// A coverage set is one document, so the set-level rules live here and the per-entry rules are
/// composed from <see cref="PackageAreaCoverageEntryValidator"/>.
/// </para>
/// <para>
/// The <c>coverages</c> member is <b>nullable</b> on purpose: an absent or explicitly null member
/// is a <b>rejected payload</b> (<c>400</c>), never an empty set — an empty set has the
/// documented, destructive meaning of clearing the tier's coverage (DEC-085).
/// </para>
/// </summary>
public class PackageAreaCoverageRequestValidator : AbstractValidator<PackageAreaCoverageRequest>
{
    private readonly IValidator<PackageAreaCoverageEntryRequest> _entryValidator;

    public PackageAreaCoverageRequestValidator(IValidator<PackageAreaCoverageEntryRequest> entryValidator)
    {
        _entryValidator = entryValidator;

        RuleFor(x => x.Coverages)
            .NotNull()
            .WithMessage("Coverages must be supplied.");

        // The per-entry rules, applied to every member of the submitted set.
        RuleForEach(x => x.Coverages!)
            .SetValidator(_entryValidator);

        // A duplicate natural key inside one submitted set is a 400, not a last-one-wins
        // (DEC-085).
        RuleFor(x => x.Coverages)
            .Must(coverages => coverages is null
                || coverages.Select(coverage => coverage.AreaId).Distinct().Count() == coverages.Count)
            .WithMessage(request => $"Area {FirstDuplicateAreaId(request)} is covered more than once.");
    }

    /// <summary>The first municipality id the submitted set covers more than once.</summary>
    private static int FirstDuplicateAreaId(PackageAreaCoverageRequest request) =>
        request.Coverages?
            .GroupBy(coverage => coverage.AreaId)
            .FirstOrDefault(group => group.Count() > 1)?
            .Key ?? 0;
}
