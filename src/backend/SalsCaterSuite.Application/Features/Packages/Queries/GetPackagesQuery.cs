using SalsCaterSuite.Application.Common.Interfaces;
using SalsCaterSuite.Application.Common.Models;
using SalsCaterSuite.Application.Features.Packages.DTOs.Request;
using SalsCaterSuite.Application.Features.Packages.DTOs.Response;
using SalsCaterSuite.Application.Features.Packages.Models;

namespace SalsCaterSuite.Application.Features.Packages.Queries;

/// <summary>GET /api/packages — list catering packages, optionally active only.</summary>
public class GetPackagesQuery
{
    private readonly IPackageRepository _packages;

    public GetPackagesQuery(IPackageRepository packages)
    {
        _packages = packages;
    }

    public async Task<Result<List<PackageResponse>>> ExecuteAsync(
        bool? activeOnly,
        int? areaId = null,
        CancellationToken cancellationToken = default)
    {
        var packages = await _packages.GetProjectionListAsync(activeOnly, areaId, cancellationToken);

        return Result<List<PackageResponse>>.Success(packages);
    }
}
