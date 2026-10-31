namespace SalsCaterSuite.Application.Features.Calendar.DTOs.Response;

public class CalendarEventResponse
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public DateTime? Start { get; set; }
    public DateTime? End { get; set; }
    public string? BackgroundColor { get; set; }
    public string? BorderColor { get; set; }
    public string? TextColor { get; set; } = "#ffffff";
    public bool AllDay { get; set; } = false;
    public ExtendedEventPropsResponse ExtendedProps { get; set; } = new();
}
