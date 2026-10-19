using SalsCaterSuite.Application.Common.Models;
using SalsCaterSuite.Application.Features.Dashboard.DTOs;
using SalsCaterSuite.Application.Features.Dashboard.Models;

namespace SalsCaterSuite.Application.Features.Dashboard.Services;

/// <summary>
/// Produces the read-only operations-dashboard aggregate for a resolved reporting period
/// (REQ-DSH-01). The service owns the aggregation; the WebApi layer only transports the result.
/// </summary>
public interface IDashboardService
{
    /// <summary>
    /// Builds the dashboard aggregate for the given period.
    /// </summary>
    /// <param name="period">The resolved reporting period.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// The aggregate, or a failed result when the dashboard cannot be produced.
    /// </returns>
    Task<Result<DashboardDto>> GetAsync(DashboardPeriod period, CancellationToken cancellationToken = default);
}
