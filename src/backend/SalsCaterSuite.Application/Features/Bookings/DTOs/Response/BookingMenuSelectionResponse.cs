using SalsCaterSuite.Domain.Enums;

namespace SalsCaterSuite.Application.Features.Bookings.DTOs.Response;

/// <summary>
/// The booking's dish selection, the tier's pools and the menu-change window
/// (contract §15.1, REQ-BKG-06, BR-35, BR-36).
/// <para>
/// The selection carries <b>no money</b> and never moves the booking's totals (BR-37). The
/// lock date is always a concrete date; only the <b>days</b> figure is <c>null</c> when the
/// configuration key is absent (DEC-128).
/// </para>
/// </summary>
public class BookingMenuSelectionResponse
{
    public int TransactionId { get; set; }
    public string BookingNumber { get; set; } = string.Empty;
    public DateOnly EventDate { get; set; }
    public int? PackageId { get; set; }
    public string? PackageName { get; set; }

    /// <summary>Always a concrete date — never <c>null</c> (DEC-128).</summary>
    public DateOnly MenuSelectionLockDate { get; set; }

    /// <summary><c>null</c> when <c>Bookings:MenuSelection:LockDaysBeforeEvent</c> is absent.</summary>
    public int? MenuSelectionLockDays { get; set; }

    public bool IsLocked { get; set; }

    /// <summary>The chosen dishes. Empty when the booking has no selection.</summary>
    public List<BookingMenuSelectionItemResponse> Selections { get; set; } = new();

    /// <summary>The tier's ruleset with its qualified pools.</summary>
    public List<BookingMenuCourseResponse> Courses { get; set; } = new();

    /// <summary>Whether the selection satisfies BR-15.</summary>
    public bool IsCompliant { get; set; }

    /// <summary><c>null</c> when it complies; otherwise it names the course and the quota.</summary>
    public string? ComplianceMessage { get; set; }

    /// <summary>
    /// The sum of the chosen dishes' food costs. <c>null</c> when nothing is selected or when any
    /// chosen dish is not costed (DEC-087).
    /// </summary>
    public decimal? SelectedFoodCostPerPax { get; set; }

    /// <summary>The tier's standard run (BR-19, DEC-083).</summary>
    public decimal? StandardRunFoodCostPerPax { get; set; }

    /// <summary>
    /// <c>(AmountPax − SelectedFoodCostPerPax) / AmountPax * 100</c>; <c>null</c> when the
    /// selected cost is unknown or the rate is null.
    /// </summary>
    public decimal? SelectedGrossProfitPercentage { get; set; }

    /// <summary>The tier's base gross profit percentage.</summary>
    public decimal? StandardRunGrossProfitPercentage { get; set; }

    /// <summary>
    /// The VAT-inclusive disclosure for the booking (DEC-143, BR-42). A memo figure disclosed
    /// <b>within</b> the net amount due — never added to it.
    /// </summary>
    public decimal? IncludedVatAmount { get; set; }

    /// <summary>The configured VAT rate, or <c>null</c> when the key is absent.</summary>
    public decimal? VatPercentage { get; set; }

    /// <summary>Exactly one of <c>None</c>, <c>Selected</c> or <c>Locked</c> (BR-28, M2).</summary>
    public BookingSelectionState SelectionState { get; set; }

    public int AddonCount { get; set; }

    /// <summary>The frozen line total of the add-ons — the amount that joins the gross.</summary>
    public decimal AddonsTotal { get; set; }

    /// <summary>
    /// The <b>derived</b> source of the frozen discount (DEC-154, BR-44). Published so the menu
    /// ledger names the rule that produced the number instead of inferring it.
    /// </summary>
    public DiscountSource DiscountSource { get; set; }

    /// <summary>
    /// The server's description of the headcount band that applied, or <c>null</c> when none did
    /// (DEC-160). The client never composes a bound.
    /// </summary>
    public string? DiscountBandLabel { get; set; }

    /// <summary>The band's rate per head, or <c>null</c> when no band applied.</summary>
    public decimal? DiscountBandRatePerHead { get; set; }
}
