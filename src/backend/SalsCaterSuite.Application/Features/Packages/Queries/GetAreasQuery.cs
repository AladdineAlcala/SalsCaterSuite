using SalsCaterSuite.Application.Common.Interfaces;
using SalsCaterSuite.Application.Common.Models;
using SalsCaterSuite.Application.Features.Packages.DTOs;

namespace SalsCaterSuite.Application.Features.Packages.Queries;

/// <summary>
/// GET /api/areas — the municipality catalogue (contract §13.9, REQ-PKG-02).
/// <para>
/// Soft-deleted municipalities are excluded — the catalogue is the population (contract §13.4).
/// </para>
/// </summary>
public class GetAreasQuery
{
    private readonly IAreaRepository _areas;

    public GetAreasQuery(IAreaRepository areas)
    {
        _areas = areas;
    }

    public async Task<Result<List<AreaDto>>> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var catalogue = await _areas.GetCatalogueAsync(cancellationToken);

        return Result<List<AreaDto>>.Success(catalogue);
    }
}
