namespace SalsCaterSuite.Application.Features.Bookings.DTOs.Response;

/// <summary>The register's own accounts receivable (KPI-39, DEC-041).</summary>
public class BookingRegisterReceivablesResponse
{
    public decimal OutstandingAmount { get; set; }

    /// <summary>The number of bookings that carry an outstanding balance.</summary>
    public int BookingCount { get; set; }

    /// <summary>The number of bookings whose event date has passed and that still owe.</summary>
    public int OverdueCount { get; set; }

    /// <summary>The amount owed by the overdue bookings.</summary>
    public decimal OverdueAmount { get; set; }
}
