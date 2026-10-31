namespace SalsCaterSuite.Application.Features.Bookings.DTOs.Response;

/// <summary>
/// The register aggregate — the four KPI cards, the payment-state distribution and the capacity
/// load (contract §14.2, KPI-33 .. KPI-39).
/// <para>
/// The summary describes the <b>same filtered population</b> as the list, so with no filter
/// applied it reconciles with the operations dashboard's KPI-12 and KPI-13 to the cent
/// (AC-BKG-05).
/// </para>
/// </summary>
public class BookingRegisterSummaryResponse
{
    /// <summary>The pipeline count and its status split (KPI-33).</summary>
    public BookingRegisterPipelineResponse Pipeline { get; set; } = new();

    /// <summary>The leading tier, its share and its period change (KPI-34).</summary>
    public BookingRegisterLeadingTierResponse? LeadingTier { get; set; }

    /// <summary>The net contracted value, the collected amount and the outstanding amount (KPI-35).</summary>
    public BookingRegisterContractValueResponse ContractValue { get; set; } = new();

    /// <summary>The committed headcount (KPI-36).</summary>
    public BookingRegisterHeadcountResponse Headcount { get; set; } = new();

    /// <summary>The payment-state distribution (KPI-37).</summary>
    public List<BookingRegisterPaymentStateResponse> PaymentStates { get; set; } = new();

    /// <summary>The near-term commissary load against the configured capacity (KPI-38).</summary>
    public BookingRegisterCapacityResponse Capacity { get; set; } = new();

    /// <summary>The register's own accounts receivable (KPI-39).</summary>
    public BookingRegisterReceivablesResponse Receivables { get; set; } = new();

    /// <summary>The menu-change window's open and closed counts (KPI-41, DEC-128).</summary>
    public BookingRegisterMenuWindowsResponse MenuWindows { get; set; } = new();

    /// <summary>The automatic headcount band's take (KPI-42, DEC-154).</summary>
    public BookingRegisterBandDiscountResponse BandDiscount { get; set; } = new();

    /// <summary>The configured VAT rate, or <c>null</c> when the key is absent (DEC-143).</summary>
    public decimal? VatPercentage { get; set; }

    /// <summary>When the aggregate was produced.</summary>
    public DateTime GeneratedAt { get; set; }
}
