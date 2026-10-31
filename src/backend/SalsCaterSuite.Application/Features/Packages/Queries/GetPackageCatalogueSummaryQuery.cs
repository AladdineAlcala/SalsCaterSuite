using SalsCaterSuite.Application.Common.Interfaces;
using SalsCaterSuite.Application.Common.Models;
using SalsCaterSuite.Application.Features.Packages.DTOs.Request;
using SalsCaterSuite.Application.Features.Packages.DTOs.Response;
using SalsCaterSuite.Application.Features.Packages.Models;

namespace SalsCaterSuite.Application.Features.Packages.Queries;

/// <summary>
/// GET /api/packages/summary — the Packages &amp; Buffets catalogue aggregate (spec §3.2,
/// DEC-068, KPI-25..KPI-28).
/// <para>
/// The use case owns the contract: the repository measures the catalogue and the use case
/// stamps the moment the measurement was taken. The aggregate is published by its <b>own</b>
/// endpoint so that the existing <c>GET /api/packages</c> keeps its shape and its
/// <c>activeOnly</c> filter, and so that no consumer has to compute a monetary aggregate in the
/// browser (DEC-002).
/// </para>
/// </summary>
public class GetPackageCatalogueSummaryQuery
{
    private readonly IPackageRepository _packages;
    private readonly IDateTime _clock;

    public GetPackageCatalogueSummaryQuery(IPackageRepository packages, IDateTime clock)
    {
        _packages = packages;
        _clock = clock;
    }

    /// <summary>
    /// The catalogue aggregate. The soft-deleted tiers are excluded by the repository's query
    /// (DEC-045 / DEC-050 — there is no global query filter), and an empty catalogue publishes
    /// zero counts with <see langword="null"/> aggregates.
    /// </summary>
    public async Task<Result<PackageCatalogueSummaryResponse>> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var summary = await _packages.GetCatalogueSummaryAsync(cancellationToken);

        summary.GeneratedAt = _clock.UtcNow;

        return Result<PackageCatalogueSummaryResponse>.Success(summary);
    }
}
