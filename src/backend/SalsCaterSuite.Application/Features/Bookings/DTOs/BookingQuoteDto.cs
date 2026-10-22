namespace SalsCaterSuite.Application.Features.Bookings.DTOs;

using SalsCaterSuite.Domain.Enums;

/// <summary>
/// The workbench quote request (contract §14.3).
/// <para>
/// The quote is a <b>what-if</b>: it prices a proposed booking and persists nothing (BR-31).
/// </para>
/// </summary>
public class BookingQuoteRequestDto
{
    /// <summary>The client.</summary>
    public int CustomerId { get; set; }

    /// <summary>The tier.</summary>
    public int PackageId { get; set; }

    /// <summary>The headcount.</summary>
    public int NoOfPerson { get; set; }

    /// <summary>The occasion.</summary>
    public string Occasion { get; set; } = string.Empty;

    /// <summary>The venue.</summary>
    public string Venue { get; set; } = string.Empty;

    /// <summary>The event date.</summary>
    public DateOnly EventDate { get; set; }

    /// <summary>The programme's start time.</summary>
    public TimeSpan StartTime { get; set; }

    /// <summary>The service's end time.</summary>
    public TimeSpan EndTime { get; set; }

    /// <summary>The kitchen call time.</summary>
    public TimeSpan? CallTime { get; set; }

    /// <summary>The pack-out time.</summary>
    public TimeSpan? PackoutTime { get; set; }

    /// <summary>The municipality.</summary>
    public int? ExtendedAreaId { get; set; }

    /// <summary>The operator's creation-time switch for the out-of-town surcharge.</summary>
    public bool ApplyExtendedAmount { get; set; }

    /// <summary>The discount code to resolve.</summary>
    public string? DiscountCode { get; set; }

    /// <summary>The deposit the operator stated.</summary>
    public decimal? DepositAmount { get; set; }

    /// <summary>The initial payment the operator intends to record.</summary>
    public decimal? InitialPaymentAmount { get; set; }
}

/// <summary>
/// The workbench quote (contract §14.3). Every figure is server-computed.
/// </summary>
public class BookingQuoteDto
{
    // ── Tier ────────────────────────────────────────────────────────────────────
    public string? PackageName { get; set; }
    public string? PackageType { get; set; }
    public int? MinPax { get; set; }
    public bool IsTierActive { get; set; }
    public decimal PricePerHead { get; set; }
    public decimal BaseAmount { get; set; }

    // ── The out-of-town surcharge ───────────────────────────────────────────────
    public int? ExtendedAreaId { get; set; }
    public string? ExtendedAreaName { get; set; }

    /// <summary>The municipality's rate; <c>null</c> when it has stated none (DEC-086).</summary>
    public decimal? ExtendedAreaRate { get; set; }

    public decimal ExtendedAreaAmount { get; set; }

    // ── The discount ────────────────────────────────────────────────────────────
    public string? DiscountCode { get; set; }
    public DiscountType DiscountType { get; set; }
    public decimal DiscountPercentage { get; set; }
    public decimal DiscountAmount { get; set; }

    // ── The money chain ─────────────────────────────────────────────────────────
    public decimal GrossAmount { get; set; }
    public decimal NetAmountDue { get; set; }

    /// <summary>The configured required deposit percentage; <c>null</c> when none is stated.</summary>
    public decimal? RequiredDepositPercentage { get; set; }

    /// <summary>The required deposit amount; <c>null</c> when none is configured.</summary>
    public decimal? RequiredDepositAmount { get; set; }

    public decimal DepositAmount { get; set; }

    /// <summary>The initial payment the operator stated.</summary>
    public decimal InitialPaymentAmount { get; set; }

    public decimal OutstandingBalance { get; set; }

    /// <summary>
    /// The advisory note about the tier's headcount floor (BR-16). It never blocks.
    /// </summary>
    public string? HeadcountFloorNote { get; set; }

    // ── The capacity gauge ──────────────────────────────────────────────────────
    public BookingRegisterCapacityDto Capacity { get; set; } = new();

    // ── The tier's planning estimates (DEC-103) ─────────────────────────────────
    public decimal? StandardRunFoodCostPerPax { get; set; }
    public decimal? BaseGrossProfitPercentage { get; set; }

    // ── The coverage verdict ────────────────────────────────────────────────────
    public BookingQuoteCoverageDto Coverage { get; set; } = new();

    // ── The five pre-dispatch checks (BR-29) ────────────────────────────────────
    public List<BookingQuoteCheckDto> Checks { get; set; } = new();
}

/// <summary>
/// The municipality coverage verdict (BR-29). <c>IsCovered</c> is <c>null</c> when the tier
/// has no coverage set — unknown, never a breach.
/// </summary>
public class BookingQuoteCoverageDto
{
    /// <summary><c>null</c> when the tier carries no coverage set.</summary>
    public bool? IsCovered { get; set; }

    /// <summary>How many municipalities the tier covers.</summary>
    public int CoveredAreaCount { get; set; }
}

/// <summary>One pre-dispatch check (BR-29).</summary>
public class BookingQuoteCheckDto
{
    /// <summary>The check's key — <c>ClientValidated</c>, <c>TierActive</c>,
    /// <c>CoursesSpecified</c>, <c>MunicipalityCovered</c> or <c>DepositMatched</c>.</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>The server-provided display label.</summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>Whether the check passes.</summary>
    public bool IsSatisfied { get; set; }

    /// <summary>Whether the check blocks. Only <c>TierActive</c> ever does.</summary>
    public bool IsBlocking { get; set; }

    /// <summary>The server-provided detail line.</summary>
    public string? Detail { get; set; }
}
