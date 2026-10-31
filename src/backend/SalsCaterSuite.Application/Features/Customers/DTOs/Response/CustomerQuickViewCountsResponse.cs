namespace SalsCaterSuite.Application.Features.Customers.DTOs.Response;

/// <summary>
/// The quick-view pills' live counts (spec §4.3, contract §10.1).
/// <para>
/// Every count describes the <b>search-filtered population with the <c>customerType</c> and
/// <c>arState</c> filters ignored</c>. <c>Corporate + Private = All</c> always holds.
/// </para>
/// </summary>
public class CustomerQuickViewCountsResponse
{
    /// <summary>Non-deleted customers matching the search filter.</summary>
    public int All { get; set; }

    /// <summary>Of that population, those whose company name is not blank (DEC-051).</summary>
    public int Corporate { get; set; }

    /// <summary>Of that population, those whose company name is blank (DEC-051).</summary>
    public int Private { get; set; }

    /// <summary>Of that population, those with a positive outstanding balance (KPI-12).</summary>
    public int WithOpenBalance { get; set; }

    /// <summary>
    /// Of that population, those whose worst aging bucket is <c>Days31To60</c>,
    /// <c>Days61To90</c> or <c>Over90Days</c> (KPI-13).
    /// </summary>
    public int Overdue30 { get; set; }
}
