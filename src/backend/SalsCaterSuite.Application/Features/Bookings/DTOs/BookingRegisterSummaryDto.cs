namespace SalsCaterSuite.Application.Features.Bookings.DTOs;

/// <summary>
/// The register aggregate — the four KPI cards, the payment-state distribution and the capacity
/// load (contract §14.2, KPI-33 .. KPI-39).
/// <para>
/// The summary describes the <b>same filtered population</b> as the list, so with no filter
/// applied it reconciles with the operations dashboard's KPI-12 and KPI-13 to the cent
/// (AC-BKG-05).
/// </para>
/// </summary>
public class BookingRegisterSummaryDto
{
    /// <summary>The pipeline count and its status split (KPI-33).</summary>
    public BookingRegisterPipelineDto Pipeline { get; set; } = new();

    /// <summary>The leading tier, its share and its period change (KPI-34).</summary>
    public BookingRegisterLeadingTierDto? LeadingTier { get; set; }

    /// <summary>The net contracted value, the collected amount and the outstanding amount (KPI-35).</summary>
    public BookingRegisterContractValueDto ContractValue { get; set; } = new();

    /// <summary>The committed headcount (KPI-36).</summary>
    public BookingRegisterHeadcountDto Headcount { get; set; } = new();

    /// <summary>The payment-state distribution (KPI-37).</summary>
    public List<BookingRegisterPaymentStateDto> PaymentStates { get; set; } = new();

    /// <summary>The near-term commissary load against the configured capacity (KPI-38).</summary>
    public BookingRegisterCapacityDto Capacity { get; set; } = new();

    /// <summary>The register's own accounts receivable (KPI-39).</summary>
    public BookingRegisterReceivablesDto Receivables { get; set; } = new();

    /// <summary>The menu-change window's open and closed counts (KPI-41, DEC-128).</summary>
    public BookingRegisterMenuWindowsDto MenuWindows { get; set; } = new();

    /// <summary>The automatic headcount band's take (KPI-42, DEC-154).</summary>
    public BookingRegisterBandDiscountDto BandDiscount { get; set; } = new();

    /// <summary>The configured VAT rate, or <c>null</c> when the key is absent (DEC-143).</summary>
    public decimal? VatPercentage { get; set; }

    /// <summary>When the aggregate was produced.</summary>
    public DateTime GeneratedAt { get; set; }
}

/// <summary>The menu-change window's open and closed counts (KPI-41, DEC-128).</summary>
public class BookingRegisterMenuWindowsDto
{
    /// <summary>The bookings whose window is still open.</summary>
    public int OpenSelectionCount { get; set; }

    /// <summary>The bookings whose window has closed.</summary>
    public int LockedSelectionCount { get; set; }
}

/// <summary>
/// The automatic headcount band's take (KPI-42, DEC-154). The count is never <c>null</c>; it is
/// <c>0</c> for an empty population.
/// </summary>
public class BookingRegisterBandDiscountDto
{
    public int DiscountedBookingCount { get; set; }

    /// <summary>The net amount due of the bookings the band priced.</summary>
    public decimal DiscountedNetAmountDue { get; set; }
}

/// <summary>The pipeline count and its status split (KPI-33).</summary>
public class BookingRegisterPipelineDto
{
    public int BookingCount { get; set; }
    public int ConfirmedCount { get; set; }
    public int TentativeCount { get; set; }
    public int CompletedCount { get; set; }
    public int OtherCount { get; set; }
    public int CommittedPax { get; set; }
}

/// <summary>The leading tier, its share and its period change (KPI-34, DEC-109).</summary>
public class BookingRegisterLeadingTierDto
{
    public string? PackageType { get; set; }
    public string? PackageName { get; set; }
    public decimal NetContractedValue { get; set; }

    /// <summary>The tier's share of the population's net contracted value.</summary>
    public decimal? SharePercentage { get; set; }

    /// <summary>
    /// The change against the immediately preceding equal-length window. <c>null</c> when the
    /// preceding window holds no booking of this tier — unknown, never zero (DEC-109).
    /// </summary>
    public decimal? ChangePercentage { get; set; }

    /// <summary>The leading tier's net contracted value in the preceding window.</summary>
    public decimal? PreviousNetContractedValue { get; set; }
}

/// <summary>The contract value of the population and its split (KPI-35).</summary>
public class BookingRegisterContractValueDto
{
    public decimal NetContractedValue { get; set; }
    public decimal CollectedAmount { get; set; }
    public decimal OutstandingAmount { get; set; }

    /// <summary>The collected share of the net contracted value; <c>null</c> when it is zero.</summary>
    public decimal? CollectionRatioPercentage { get; set; }
}

/// <summary>The committed headcount of the population (KPI-36).</summary>
public class BookingRegisterHeadcountDto
{
    public int TotalPax { get; set; }

    /// <summary>The mean headcount per booking; <c>null</c> for an empty population.</summary>
    public decimal? AveragePaxPerBooking { get; set; }

    /// <summary>The next event's date; <c>null</c> when the population has no future event.</summary>
    public DateOnly? NextEventDate { get; set; }

    /// <summary>The next event's booking number.</summary>
    public string? NextEventBookingNumber { get; set; }
}

/// <summary>One payment state's count and its outstanding amount (KPI-37).</summary>
public class BookingRegisterPaymentStateDto
{
    public string Key { get; set; } = string.Empty;
    public int Count { get; set; }
    public decimal Amount { get; set; }
}

/// <summary>The near-term commissary load against the configured capacity (KPI-38, DEC-102).</summary>
public class BookingRegisterCapacityDto
{
    /// <summary>The pax committed in the next 14 days.</summary>
    public int CommittedPax { get; set; }

    /// <summary>
    /// The configured capacity. <c>null</c> when the key is absent — unknown, never zero
    /// (DEC-102).
    /// </summary>
    public int? CapacityPax { get; set; }

    /// <summary>The utilisation; <c>null</c> when the capacity is unknown.</summary>
    public decimal? UtilisationPercentage { get; set; }

    /// <summary>Whether a capacity has been configured at all.</summary>
    public bool IsConfigured { get; set; }
}

/// <summary>The register's own accounts receivable (KPI-39, DEC-041).</summary>
public class BookingRegisterReceivablesDto
{
    public decimal OutstandingAmount { get; set; }

    /// <summary>The number of bookings that carry an outstanding balance.</summary>
    public int BookingCount { get; set; }

    /// <summary>The number of bookings whose event date has passed and that still owe.</summary>
    public int OverdueCount { get; set; }

    /// <summary>The amount owed by the overdue bookings.</summary>
    public decimal OverdueAmount { get; set; }
}
