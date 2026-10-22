using FluentValidation;
using SalsCaterSuite.Application.Common.Interfaces;
using SalsCaterSuite.Application.Common.Models;
using SalsCaterSuite.Application.Features.Packages.DTOs;
using SalsCaterSuite.Application.Features.Packages.Queries;
using SalsCaterSuite.Domain.Entities;

namespace SalsCaterSuite.Application.Features.Packages.Commands;

/// <summary>
/// PUT /api/packages/{id}/areas — replace the tier's coverage set (contract §13.5, REQ-PKG-02).
/// <para>
/// A coverage set is one document, so the submitted set <b>replaces</b> the tier's coverage: the
/// submitted entries are upserted on their natural key (the municipality), the entries that were
/// not submitted are <b>soft-deleted</b> — never destroyed (DEC-085) — and the whole change
/// commits in one <c>SaveChangesAsync</c> (DEC-039).
/// </para>
/// <para>
/// A re-added municipality <b>resurrects</b> its soft-deleted row instead of inserting a second
/// one (DEC-088). An <b>empty</b> set clears the tier's coverage; a <c>null</c> set is a rejected
/// payload.
/// </para>
/// </summary>
public class ReplacePackageAreaCoverageCommand
{
    private readonly IPackageRepository _packages;
    private readonly IPackageAreaCoverageRepository _coverages;
    private readonly IValidator<PackageAreaCoverageRequest> _validator;
    private readonly GetPackageAreaCoverageMatrixQuery _matrix;

    public ReplacePackageAreaCoverageCommand(
        IPackageRepository packages,
        IPackageAreaCoverageRepository coverages,
        IValidator<PackageAreaCoverageRequest> validator,
        GetPackageAreaCoverageMatrixQuery matrix)
    {
        _packages = packages;
        _coverages = coverages;
        _validator = validator;
        _matrix = matrix;
    }

    public async Task<Result<PackageAreaCoverageMatrixDto>> ExecuteAsync(
        int packageId,
        PackageAreaCoverageRequest request,
        CancellationToken cancellationToken = default)
    {
        // The validation runs first, so a rejected payload never reaches the database (BR-11).
        var validationResult = await _validator.ValidateAsync(request, cancellationToken);

        if (!validationResult.IsValid)
        {
            return Result<PackageAreaCoverageMatrixDto>.Failure(
                validationResult.Errors.Select(error => error.ErrorMessage).ToArray());
        }

        // The tier is resolved through the catalogue population: a soft-deleted tier is refused
        // by the write (DEC-074).
        var package = await _packages.GetProjectionByIdAsync(packageId, cancellationToken);

        if (package is null)
        {
            return new PackageNotFoundResult<PackageAreaCoverageMatrixDto>("Package not found.");
        }

        // Every row the tier holds, the soft-deleted ones included, so a re-added municipality
        // can resurrect its row (DEC-088).
        var existing = await _coverages.GetAllCoveragesAsync(packageId, cancellationToken);
        var byArea = existing.ToLookup(coverage => coverage.AreaId);

        var submitted = request.Coverages!.ToList();
        var submittedAreaIds = submitted.Select(coverage => coverage.AreaId).ToHashSet();

        foreach (var entry in submitted)
        {
            var match = byArea[entry.AreaId].FirstOrDefault(coverage => !coverage.IsDeleted)
                        ?? byArea[entry.AreaId].FirstOrDefault();

            if (match is null)
            {
                await _coverages.AddAsync(new PackageAreaCoverage
                {
                    PackageId = packageId,
                    AreaId = entry.AreaId,
                    IsActive = entry.IsActive
                }, cancellationToken);

                continue;
            }

            match.IsActive = entry.IsActive;

            // The resurrect: a re-added natural key revives its soft-deleted row.
            match.IsDeleted = false;
        }

        // The rows that were not submitted are soft-deleted, never destroyed (DEC-085).
        foreach (var coverage in existing.Where(coverage => !coverage.IsDeleted && !submittedAreaIds.Contains(coverage.AreaId)))
        {
            coverage.IsDeleted = true;
        }

        await _coverages.SaveChangesAsync(cancellationToken);

        // The response is the new state, read back through the one matrix projection.
        return await _matrix.ExecuteAsync(packageId, cancellationToken);
    }
}
