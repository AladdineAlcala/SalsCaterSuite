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

    /// <summary>
    /// The proposed dish selection, in exactly the create request's shape (contract §15.3).
    /// The quote prices it through the same code path the create uses (BR-31, DEC-153).
    /// </summary>
    public List<BookingMenuSelectionEntryDto>? Selections { get; set; }

    /// <summary>
    /// The proposed add-on lines, in exactly the create request's shape (contract §16.4).
    /// </summary>
    public List<BookingAddonRequestDto>? Addons { get; set; }
}

/// <summary>
/// The workbench quote (contract §14.3). Every figure is server-computed.
/// </summary>
public class BookingQuoteDto
{
    // ── Tier ────────────────────────────────────────────────────────────────────
    public string? PackageName { get; set; }

    /// <summary>The tier's human-readable code (the owner's 2026-10-24 request).</summary>
    public string? PackageCode { get; set; }

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

    /// <summary>
    /// The tier's own per-guest extended-location amount; <c>null</c> = not stated (the owner's
    /// 2026-10-24 request).
    /// </summary>
    public decimal? PackageExtendedLocationRate { get; set; }

    public decimal ExtendedAreaAmount { get; set; }

    // ── The discount ────────────────────────────────────────────────────────────
    public string? DiscountCode { get; set; }
    public DiscountType DiscountType { get; set; }
    public decimal DiscountPercentage { get; set; }
    public decimal DiscountAmount { get; set; }

    /// <summary>The <b>derived</b> source of the discount (BR-44, DEC-154).</summary>
    public DiscountSource DiscountSource { get; set; }

    /// <summary>The applied band's label, or <c>null</c> when no band applied (DEC-160).</summary>
    public string? DiscountBandLabel { get; set; }

    /// <summary>The applied band's rate per head, or <c>null</c> when no band applied.</summary>
    public decimal? DiscountBandRatePerHead { get; set; }

    // ── The money chain ─────────────────────────────────────────────────────────
    public decimal GrossAmount { get; set; }
    public decimal NetAmountDue { get; set; }

    /// <summary>The frozen add-on line total that joined the gross (DEC-135, DEC-151).</summary>
    public decimal AddonsTotal { get; set; }

    /// <summary>
    /// The VAT-inclusive disclosure — a memo figure <b>within</b> the net amount due (BR-42,
    /// DEC-143). <c>null</c> when the rate is absent or the net is zero.
    /// </summary>
    public decimal? IncludedVatAmount { get; set; }

    /// <summary>The configured VAT rate, or <c>null</c> when the key is absent.</summary>
    public decimal? VatPercentage { get; set; }

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

    // ── The tier's course rules and the proposed selection (contract §14.3) ────
    /// <summary>
    /// The tier's own rules, one entry per rule, in the tier's order. <c>qualifiedPool</c> is the
    /// <b>count</b> of live dishes in the rule's pool.
    /// </summary>
    public List<BookingQuoteCourseDto> Courses { get; set; } = new();

    /// <summary>The selection the request asks for (contract §15.1's shape).</summary>
    public BookingQuoteSelectionDto Selection { get; set; } = new();
}

/// <summary>
/// One course rule of the tier as the quote publishes it (contract §14.3). Unlike the selection
/// read's <see cref="BookingMenuCourseDto"/> it carries the pool's <b>count</b>, not the pool.
/// </summary>
public class BookingQuoteCourseDto
{
    public int CourseId { get; set; }
    public string? CourseName { get; set; }
    public int RequiredQuota { get; set; }
    public int? PortionGrams { get; set; }

    /// <summary><c>PortionGrams x NumberOfHeads / 1000</c> (DEC-145); <c>null</c> when no portion.</summary>
    public decimal? BatchMassKg { get; set; }

    public string? RuleDescription { get; set; }

    /// <summary>The number of live dishes in the rule's qualified pool.</summary>
    public int QualifiedPool { get; set; }
}

/// <summary>
/// The proposed selection a quote describes (contract §14.3) — the same shape
/// <c>GET /api/bookings/{id}/menus</c> uses for a stored selection.
/// </summary>
public class BookingQuoteSelectionDto
{
    public bool IsLocked { get; set; }

    /// <summary>Always a concrete date — never <c>null</c> (DEC-128).</summary>
    public DateOnly LockDate { get; set; }

    public List<BookingQuoteSelectionCourseDto> Courses { get; set; } = new();

    /// <summary>The sum of the chosen dishes' food costs, or <c>null</c> when unknown.</summary>
    public decimal? SelectedFoodCostPerPax { get; set; }

    /// <summary>The selection's planning margin, or <c>null</c> when either input is unknown.</summary>
    public decimal? SelectedGrossProfitPercentage { get; set; }
}

/// <summary>One course of the proposed selection (contract §14.3).</summary>
public class BookingQuoteSelectionCourseDto
{
    public int CourseId { get; set; }
    public string? CourseName { get; set; }
    public List<BookingQuoteSelectedMenuDto> SelectedMenus { get; set; } = new();
}

/// <summary>One chosen dish of the proposed selection (contract §14.3).</summary>
public class BookingQuoteSelectedMenuDto
{
    public string MenuId { get; set; } = string.Empty;
    public string? MenuName { get; set; }
    public string? Note { get; set; }
    public string? PrepInstruction { get; set; }
    public decimal? FoodCost { get; set; }
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
