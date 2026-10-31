namespace SalsCaterSuite.Application.Features.Customers.DTOs.Response;

/// <summary>
/// The directory's KPI cards (spec §4.2).
/// </summary>
public class CustomerDirectorySummaryResponse
{
    /// <summary>KPI-20 — the number of registered (non-deleted) customers in the filtered set.</summary>
    public int TotalClients { get; set; }

    /// <summary>
    /// KPI-12 — <c>Σ max(0, NetAmountDue − AmountPaid)</c> over the filtered set's
    /// non-cancelled bookings (DEC-025, BR-03).
    /// </summary>
    public decimal ActiveArAmount { get; set; }

    /// <summary>The number of filtered customers that carry a positive balance.</summary>
    public int ClientsWithBalance { get; set; }

    /// <summary>KPI-21 — <c>Σ NetAmountDue</c> over the filtered set's non-cancelled bookings.</summary>
    public decimal TotalContractedValue { get; set; }

    /// <summary>
    /// <c>TotalContractedValue ÷ TotalClients</c>, or <c>null</c> when there are no clients.
    /// A <c>null</c> means "undefined" — never <c>0</c> (contract §9.2 rule 6, "no null-as-zero").
    /// </summary>
    public decimal? AverageValuePerClient { get; set; }

    /// <summary>
    /// KPI-13 — the outstanding balance of the filtered customers whose aging state is more
    /// than 30 days overdue.
    /// </summary>
    public decimal OverdueAmount { get; set; }

    /// <summary>The number of filtered customers whose aging state is more than 30 days overdue.</summary>
    public int OverdueClientCount { get; set; }
}
