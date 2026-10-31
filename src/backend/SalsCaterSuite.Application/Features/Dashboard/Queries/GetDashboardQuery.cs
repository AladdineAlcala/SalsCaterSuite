using SalsCaterSuite.Application.Common.Interfaces;
using SalsCaterSuite.Application.Common.Models;
using SalsCaterSuite.Application.Features.Dashboard.DTOs.Response;
using SalsCaterSuite.Application.Features.Dashboard.Models;
using SalsCaterSuite.Application.Features.Dashboard.Services;

namespace SalsCaterSuite.Application.Features.Dashboard.Queries;

/// <summary>
/// GET /api/dashboard — the operations-dashboard aggregate (REQ-DSH-01).
/// <para>
/// The use case owns the transport-level validation of the optional <c>period</c> query
/// string: it must be exactly <c>YYYY-MM</c>. A malformed value is a business/validation
/// failure and is reported as <c>400 { "errors": [ ... ] }</c> (contract §7, §9).
/// </para>
/// </summary>
public class GetDashboardQuery
{
    private readonly IDashboardService _dashboard;
    private readonly IDateTime _dateTime;

    public GetDashboardQuery(IDashboardService dashboard, IDateTime dateTime)
    {
        _dashboard = dashboard;
        _dateTime = dateTime;
    }

    /// <summary>
    /// Builds the dashboard aggregate for the requested period, or the current UTC calendar
    /// month when no period is supplied.
    /// </summary>
    /// <param name="period">The optional <c>YYYY-MM</c> period query string.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<Result<DashboardResponse>> ExecuteAsync(string? period, CancellationToken cancellationToken = default)
    {
        DashboardPeriod resolvedPeriod;

        if (string.IsNullOrWhiteSpace(period))
        {
            resolvedPeriod = DashboardPeriod.FromUtcNow(_dateTime.UtcNow);
        }
        else if (!DashboardPeriod.TryParse(period, out resolvedPeriod))
        {
            return Result<DashboardResponse>.Failure(DashboardErrors.InvalidPeriod);
        }

        return await _dashboard.GetAsync(resolvedPeriod, cancellationToken);
    }
}
