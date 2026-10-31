namespace SalsCaterSuite.Application.Features.Bookings.DTOs.Response;

/// <summary>The contract value of the population and its split (KPI-35).</summary>
public class BookingRegisterContractValueResponse
{
    public decimal NetContractedValue { get; set; }
    public decimal CollectedAmount { get; set; }
    public decimal OutstandingAmount { get; set; }

    /// <summary>The collected share of the net contracted value; <c>null</c> when it is zero.</summary>
    public decimal? CollectionRatioPercentage { get; set; }
}
