namespace SalsCaterSuite.Application.Features.Calendar.DTOs;

public class CalendarEventDto
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public DateTime? Start { get; set; }
    public DateTime? End { get; set; }
    public string? BackgroundColor { get; set; }
    public string? BorderColor { get; set; }
    public string? TextColor { get; set; } = "#ffffff";
    public bool AllDay { get; set; } = false;
    public ExtendedEventProps ExtendedProps { get; set; } = new();
}

public class ExtendedEventProps
{
    public int TransactionId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string Venue { get; set; } = string.Empty;
    public int? Pax { get; set; }
    public string? PackageName { get; set; }
    public string Status { get; set; } = "Confirmed";
    public decimal BalanceDue { get; set; }
}

