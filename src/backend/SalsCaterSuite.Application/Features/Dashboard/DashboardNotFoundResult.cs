using SalsCaterSuite.Application.Common.Models;
using SalsCaterSuite.Application.Features.Dashboard.DTOs.Response;
using SalsCaterSuite.Application.Features.Dashboard.Models;

namespace SalsCaterSuite.Application.Features.Dashboard;

/// <summary>
/// A dashboard failure that must be surfaced as HTTP 404 (contract §7).
/// <para>
/// The dashboard's only current failure mode is a malformed <c>period</c>, which is a
/// validation failure and maps to HTTP 400. This type exists so that the WebApi layer can map a
/// "not found" style failure to the correct status code <b>without</b> inspecting the error
/// message text, which the error contract explicitly advises against.
/// </para>
/// </summary>
public sealed class DashboardNotFoundResult : Result<DashboardResponse>
{
    public DashboardNotFoundResult(string error)
        : base(false, null, new[] { error })
    {
    }
}
