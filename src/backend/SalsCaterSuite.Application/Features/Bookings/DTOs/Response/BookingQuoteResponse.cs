using SalsCaterSuite.Domain.Enums;

namespace SalsCaterSuite.Application.Features.Bookings.DTOs.Response;

/// <summary>
/// The workbench quote (contract §14.3). Every figure is server-computed.
/// </summary>
public class BookingQuoteResponse
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
    public BookingRegisterCapacityResponse Capacity { get; set; } = new();

    // ── The tier's planning estimates (DEC-103) ─────────────────────────────────
    public decimal? StandardRunFoodCostPerPax { get; set; }
    public decimal? BaseGrossProfitPercentage { get; set; }

    // ── The coverage verdict ────────────────────────────────────────────────────
    public BookingQuoteCoverageResponse Coverage { get; set; } = new();

    // ── The five pre-dispatch checks (BR-29) ────────────────────────────────────
    public List<BookingQuoteCheckResponse> Checks { get; set; } = new();

    // ── The tier's course rules and the proposed selection (contract §14.3) ────
    /// <summary>
    /// The tier's own rules, one entry per rule, in the tier's order. <c>qualifiedPool</c> is the
    /// <b>count</b> of live dishes in the rule's pool.
    /// </summary>
    public List<BookingQuoteCourseResponse> Courses { get; set; } = new();

    /// <summary>The selection the request asks for (contract §15.1's shape).</summary>
    public BookingQuoteSelectionResponse Selection { get; set; } = new();
}
