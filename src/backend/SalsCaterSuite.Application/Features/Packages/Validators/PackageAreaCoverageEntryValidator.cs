using FluentValidation;
using SalsCaterSuite.Application.Common.Interfaces;
using SalsCaterSuite.Application.Features.Packages.DTOs;

namespace SalsCaterSuite.Application.Features.Packages.Validators;

/// <summary>
/// The validation rules of <b>one</b> submitted coverage (spec §3.6, BR-11, DEC-077).
/// <para>
/// The set validator composes this one, so the two can never drift apart.
/// </para>
/// <para>
/// The existence rule treats a <b>soft-deleted</b> municipality as "does not exist", so the
/// lookup filters <c>IsDeleted</c> (spec §3.6).
/// </para>
/// </summary>
public class PackageAreaCoverageEntryValidator : AbstractValidator<PackageAreaCoverageEntryDto>
{
    private readonly IAreaRepository _areas;

    public PackageAreaCoverageEntryValidator(IAreaRepository areas)
    {
        _areas = areas;

        RuleFor(x => x.AreaId)
            .MustAsync(async (areaId, cancellationToken) =>
                await _areas.ExistsAsync(areaId, cancellationToken))
            .WithMessage((entry, areaId) => $"Area {areaId} does not exist.");
    }
}
