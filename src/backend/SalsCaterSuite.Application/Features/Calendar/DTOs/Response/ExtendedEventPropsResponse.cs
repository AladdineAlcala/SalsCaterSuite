namespace SalsCaterSuite.Application.Features.Calendar.DTOs.Response;

public class ExtendedEventPropsResponse
{
    public int TransactionId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string Venue { get; set; } = string.Empty;
    public int? Pax { get; set; }
    public string? PackageName { get; set; }
    public string Status { get; set; } = "Confirmed";
    public decimal BalanceDue { get; set; }
}
