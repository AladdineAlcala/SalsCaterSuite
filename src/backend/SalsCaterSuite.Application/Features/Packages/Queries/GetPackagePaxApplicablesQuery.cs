using SalsCaterSuite.Application.Common.Interfaces;
using SalsCaterSuite.Application.Common.Models;
using SalsCaterSuite.Application.Features.Packages.DTOs.Request;
using SalsCaterSuite.Application.Features.Packages.DTOs.Response;
using SalsCaterSuite.Application.Features.Packages.Models;

namespace SalsCaterSuite.Application.Features.Packages.Queries;

/// <summary>
/// <c>GET /api/packages/pax-applicables</c> — the five fixed applicable-pax classification
/// options (REQ-PKG-04, DEC-173, contract §12.8).
/// <para>
/// The endpoint is read-only: the lookup ships fixed reference data and exposes no
/// administration surface. The options are returned in ascending identifier order, so the
/// client can render the selector straight from the response without re-sorting it.
/// </para>
/// </summary>
public class GetPackagePaxApplicablesQuery
{
    private readonly IPackagePaxApplicableRepository _paxApplicables;

    public GetPackagePaxApplicablesQuery(IPackagePaxApplicableRepository paxApplicables)
    {
        _paxApplicables = paxApplicables;
    }

    public async Task<Result<List<PackagePaxApplicableResponse>>> ExecuteAsync(
        CancellationToken cancellationToken = default)
    {
        var rows = await _paxApplicables.GetAllAsync(cancellationToken);

        return Result<List<PackagePaxApplicableResponse>>.Success(rows
            .Select(row => new PackagePaxApplicableResponse
            {
                NumPaxId = row.NumPaxId,
                PackageApplicablePax = row.PackageApplicablePax
            })
            .ToList());
    }
}
