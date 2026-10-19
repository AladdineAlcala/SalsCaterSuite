namespace SalsCaterSuite.Application.Features.Dashboard;

/// <summary>
/// The dashboard's business-failure messages (contract §7, §9). They are declared once so the
/// Application layer, the WebApi layer and the tests all assert against the same literal.
/// </summary>
public static class DashboardErrors
{
    /// <summary>The <c>period</c> query string is not in the <c>YYYY-MM</c> format (HTTP 400).</summary>
    public const string InvalidPeriod = "Period must be in the format YYYY-MM.";
}
