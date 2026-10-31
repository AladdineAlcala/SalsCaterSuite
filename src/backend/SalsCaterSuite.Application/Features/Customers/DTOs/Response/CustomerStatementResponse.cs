namespace SalsCaterSuite.Application.Features.Customers.DTOs.Response;

public class CustomerStatementResponse
{
    public int CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string? Contact { get; set; }
    public string? Address { get; set; }
    public decimal TotalBilled { get; set; }
    public decimal TotalPaid { get; set; }

    /// <summary>
    /// <c>Σ Refund.NetAmount</c> over the customer's non-cancelled bookings. Refunds are
    /// reported separately and never reduce <see cref="OutstandingBalance"/> (DEC-013).
    /// </summary>
    public decimal TotalRefunded { get; set; }

    public decimal OutstandingBalance { get; set; }

    /// <summary>The customer's aging state key (spec §3, DEC-052).</summary>
    public string LedgerState { get; set; } = string.Empty;

    /// <summary>The server-provided display label of <see cref="LedgerState"/>.</summary>
    public string LedgerStateLabel { get; set; } = string.Empty;

    public List<CustomerStatementLineResponse> Transactions { get; set; } = new();
}
