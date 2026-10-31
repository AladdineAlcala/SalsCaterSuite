using SalsCaterSuite.Application.Common.Interfaces;
using SalsCaterSuite.Application.Common.Models;
using SalsCaterSuite.Application.Features.Packages.DTOs.Request;
using SalsCaterSuite.Application.Features.Packages.DTOs.Response;
using SalsCaterSuite.Application.Features.Packages.Models;

namespace SalsCaterSuite.Application.Features.Packages.Queries;

/// <summary>GET /api/packages/{id} — a single package.</summary>
public class GetPackageByIdQuery
{
    private readonly IPackageRepository _packages;

    public GetPackageByIdQuery(IPackageRepository packages)
    {
        _packages = packages;
    }

    public async Task<Result<PackageResponse>> ExecuteAsync(int id, CancellationToken cancellationToken = default)
    {
        var package = await _packages.GetProjectionByIdAsync(id, cancellationToken);

        return package is null
            ? Result<PackageResponse>.Failure("Package not found.")
            : Result<PackageResponse>.Success(package);
    }
}
