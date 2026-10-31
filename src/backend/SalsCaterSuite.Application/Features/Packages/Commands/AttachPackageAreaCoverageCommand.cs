using SalsCaterSuite.Application.Common.Interfaces;
using SalsCaterSuite.Application.Common.Models;
using SalsCaterSuite.Application.Features.Packages.DTOs.Request;
using SalsCaterSuite.Application.Features.Packages.DTOs.Response;
using SalsCaterSuite.Application.Features.Packages.Models;
using SalsCaterSuite.Domain.Entities;

namespace SalsCaterSuite.Application.Features.Packages.Commands;

/// <summary>
/// POST /api/packages/{id}/areas — attach a municipality to the tier (contract §13.6, REQ-PKG-02).
/// <para>
/// The tier is resolved first, so a soft-deleted tier answers <c>404</c> and is never written to
/// (DEC-074). An unknown or soft-deleted municipality is a <c>400</c>, and a municipality the
/// tier already covers is a <c>400</c> too — the coverage set holds at most one live row per
/// municipality (DEC-085, DEC-088).
/// </para>
/// </summary>
public class AttachPackageAreaCoverageCommand
{
    private readonly IPackageRepository _packages;
    private readonly IPackageAreaCoverageRepository _coverages;
    private readonly IAreaRepository _areas;

    public AttachPackageAreaCoverageCommand(
        IPackageRepository packages,
        IPackageAreaCoverageRepository coverages,
        IAreaRepository areas)
    {
        _packages = packages;
        _coverages = coverages;
        _areas = areas;
    }

    public async Task<Result<int>> ExecuteAsync(
        int packageId,
        PackageAreaCoverageAttachRequest request,
        CancellationToken cancellationToken = default)
    {
        // The tier is resolved through the catalogue population (DEC-074).
        var package = await _packages.GetProjectionByIdAsync(packageId, cancellationToken);

        if (package is null)
        {
            return new PackageNotFoundResult<int>("Package not found.");
        }

        // A soft-deleted municipality does not exist for the workbench (spec §3.6).
        if (!await _areas.ExistsAsync(request.AreaId, cancellationToken))
        {
            return Result<int>.Failure($"Area {request.AreaId} does not exist.");
        }

        var coverages = await _coverages.GetLiveCoveragesAsync(packageId, cancellationToken);

        if (coverages.Any(coverage => coverage.AreaId == request.AreaId))
        {
            return Result<int>.Failure($"Area {request.AreaId} is already covered by this package.");
        }

        var coverage = new PackageAreaCoverage
        {
            PackageId = packageId,
            AreaId = request.AreaId,
            IsActive = request.IsActive
        };

        await _coverages.AddAsync(coverage, cancellationToken);
        await _coverages.SaveChangesAsync(cancellationToken);

        return Result<int>.Success(coverage.PackageAreaCoverageId);
    }
}
