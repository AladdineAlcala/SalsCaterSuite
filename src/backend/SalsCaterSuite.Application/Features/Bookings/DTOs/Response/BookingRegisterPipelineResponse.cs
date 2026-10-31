namespace SalsCaterSuite.Application.Features.Bookings.DTOs.Response;

/// <summary>The pipeline count and its status split (KPI-33).</summary>
public class BookingRegisterPipelineResponse
{
    public int BookingCount { get; set; }
    public int ConfirmedCount { get; set; }
    public int TentativeCount { get; set; }
    public int CompletedCount { get; set; }
    public int OtherCount { get; set; }
    public int CommittedPax { get; set; }
}
